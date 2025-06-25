using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace CatiaMonitor.Server
{
    /// <summary>
    /// 웹 대시보드에 표시될 클라이언트 상태 요약 정보 모델입니다.
    /// Status 필드를 추가하여 상태를 더 명확하게 관리합니다.
    /// </summary>
    public class ClientStatusSummary
    {
        public string IpAddress { get; set; } = string.Empty;
        public string Status { get; set; } = "Unreachable"; // 상태: "Running", "Offline", "Unreachable"
        public string LastLogTime { get; set; } = string.Empty;
        public string LastSeen { get; set; } = string.Empty;
    }

    /// <summary>
    /// Manages all database operations using SQLite.
    /// SQLite를 사용하여 모든 데이터베이스 작업을 관리합니다.
    /// </summary>
    public class DatabaseManager
    {
        private readonly string _connectionString;

        public DatabaseManager(string dbFileName = "catia_monitor.db")
        {
            string dbPath = Path.Combine(AppContext.BaseDirectory, dbFileName);
            _connectionString = $"Data Source={dbPath}";
        }

        public async Task InitializeDatabaseAsync()
        {
            using (var connection = new SqliteConnection(_connectionString))
            {
                await connection.OpenAsync();

                var createClientsTable = @"
                    CREATE TABLE IF NOT EXISTS Clients (
                        ClientId    INTEGER PRIMARY KEY AUTOINCREMENT,
                        IpAddress   TEXT NOT NULL UNIQUE,
                        LastSeen    TEXT NOT NULL
                    );";

                using (var command = new SqliteCommand(createClientsTable, connection))
                    await command.ExecuteNonQueryAsync();

                var createUsageLogsTable = @"
                    CREATE TABLE IF NOT EXISTS UsageLogs (
                        LogId           INTEGER PRIMARY KEY AUTOINCREMENT,
                        ClientId        INTEGER NOT NULL,
                        Timestamp       TEXT NOT NULL,
                        IsCatiaRunning  INTEGER NOT NULL,
                        FOREIGN KEY(ClientId) REFERENCES Clients(ClientId)
                    );";

                using (var command = new SqliteCommand(createUsageLogsTable, connection))
                    await command.ExecuteNonQueryAsync();
            }
            Console.WriteLine("[Database] Database initialized successfully.");
        }

        public async Task<int> EnsureClientExists(string ipAddress)
        {
            using (var connection = new SqliteConnection(_connectionString))
            {
                await connection.OpenAsync();

                var selectCmd = new SqliteCommand("SELECT ClientId FROM Clients WHERE IpAddress = @ip", connection);
                selectCmd.Parameters.AddWithValue("@ip", ipAddress);

                var clientId = await selectCmd.ExecuteScalarAsync();
                string currentTime = DateTime.UtcNow.ToString("o"); // ISO 8601 format

                if (clientId != null)
                {
                    var updateCmd = new SqliteCommand("UPDATE Clients SET LastSeen = @time WHERE ClientId = @id", connection);
                    updateCmd.Parameters.AddWithValue("@time", currentTime);
                    updateCmd.Parameters.AddWithValue("@id", Convert.ToInt32(clientId));
                    await updateCmd.ExecuteNonQueryAsync();
                    return Convert.ToInt32(clientId);
                }
                else
                {
                    var insertCmd = new SqliteCommand("INSERT INTO Clients (IpAddress, LastSeen) VALUES (@ip, @time); SELECT last_insert_rowid();", connection);
                    insertCmd.Parameters.AddWithValue("@ip", ipAddress);
                    insertCmd.Parameters.AddWithValue("@time", currentTime);

                    var newClientId = await insertCmd.ExecuteScalarAsync();
                    return Convert.ToInt32(newClientId);
                }
            }
        }

        public async Task LogUsage(int clientId, bool isCatiaRunning)
        {
            using (var connection = new SqliteConnection(_connectionString))
            {
                await connection.OpenAsync();
                string currentTime = DateTime.UtcNow.ToString("o");

                var updateClientCmd = new SqliteCommand("UPDATE Clients SET LastSeen = @time WHERE ClientId = @id", connection);
                updateClientCmd.Parameters.AddWithValue("@time", currentTime);
                updateClientCmd.Parameters.AddWithValue("@id", clientId);
                await updateClientCmd.ExecuteNonQueryAsync();

                var insertLogCmd = new SqliteCommand("INSERT INTO UsageLogs (ClientId, Timestamp, IsCatiaRunning) VALUES (@id, @time, @status)", connection);
                insertLogCmd.Parameters.AddWithValue("@id", clientId);
                insertLogCmd.Parameters.AddWithValue("@time", currentTime);
                insertLogCmd.Parameters.AddWithValue("@status", isCatiaRunning ? 1 : 0);

                await insertLogCmd.ExecuteNonQueryAsync();
            }
        }

        /// <summary>
        /// <<★★★ 로직 대폭 수정 ★★★>>
        /// 웹 대시보드를 위해 모든 클라이언트의 최신 상태를 조회합니다.
        /// 3분 이상 응답이 없는 클라이언트는 "Unreachable"로 처리합니다.
        /// </summary>
        public async Task<List<ClientStatusSummary>> GetClientStatusSummaryAsync()
        {
            var summaryList = new List<ClientStatusSummary>();
            using (var connection = new SqliteConnection(_connectionString))
            {
                await connection.OpenAsync();

                var commandText = @"
                    SELECT 
                        c.IpAddress, 
                        c.LastSeen, 
                        ul.IsCatiaRunning, 
                        ul.Timestamp as LastLogTime
                    FROM Clients c
                    LEFT JOIN UsageLogs ul ON ul.LogId = (
                        SELECT MAX(LogId) 
                        FROM UsageLogs 
                        WHERE ClientId = c.ClientId
                    )
                    ORDER BY c.IpAddress";

                var command = new SqliteCommand(commandText, connection);
                using (var reader = await command.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        var summary = new ClientStatusSummary
                        {
                            IpAddress = reader.GetString(0),
                            LastSeen = reader.GetString(1),
                            LastLogTime = !reader.IsDBNull(3) ? reader.GetString(3) : "N/A"
                        };

                        // 최종 상태를 결정하는 로직
                        var lastSeenTime = DateTime.Parse(summary.LastSeen, null, System.Globalization.DateTimeStyles.RoundtripKind);

                        // 마지막으로 목격된 시간이 3분을 초과하면 '연결 끊김'으로 간주
                        if ((DateTime.UtcNow - lastSeenTime).TotalMinutes > 3)
                        {
                            summary.Status = "Unreachable";
                        }
                        // 로그가 없으면(최초 연결 중) '연결 중'으로 표시
                        else if (reader.IsDBNull(2))
                        {
                            summary.Status = "Connecting...";
                        }
                        // 로그가 있으면 CATIA 실행 여부에 따라 상태 결정
                        else
                        {
                            bool isCatiaRunning = reader.GetInt32(2) == 1;
                            summary.Status = isCatiaRunning ? "Running" : "Offline";
                        }

                        summaryList.Add(summary);
                    }
                }
            }
            return summaryList;
        }
    }
}
