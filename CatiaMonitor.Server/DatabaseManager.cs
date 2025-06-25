using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace CatiaMonitor.Server
{
    /// <summary>
    /// Represents the summary status of a client for the web dashboard.
    /// 웹 대시보드에 표시될 클라이언트 상태 요약 정보 모델입니다.
    /// </summary>
    public class ClientStatusSummary
    {
        public int ClientId { get; set; }
        public string IpAddress { get; set; } = string.Empty;
        public string LastSeen { get; set; } = string.Empty;
        public bool IsCatiaRunning { get; set; }
        public string LastLogTime { get; set; } = string.Empty;
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
                    // 클라이언트가 존재하면 LastSeen 시간만 업데이트합니다.
                    var updateCmd = new SqliteCommand("UPDATE Clients SET LastSeen = @time WHERE ClientId = @id", connection);
                    updateCmd.Parameters.AddWithValue("@time", currentTime);
                    updateCmd.Parameters.AddWithValue("@id", Convert.ToInt32(clientId));
                    await updateCmd.ExecuteNonQueryAsync();
                    return Convert.ToInt32(clientId);
                }
                else
                {
                    // 클라이언트가 없으면 새로 추가합니다.
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

                // 클라이언트 테이블의 LastSeen도 함께 업데이트하여 마지막 통신 시간을 기록합니다.
                var updateClientCmd = new SqliteCommand("UPDATE Clients SET LastSeen = @time WHERE ClientId = @id", connection);
                updateClientCmd.Parameters.AddWithValue("@time", currentTime);
                updateClientCmd.Parameters.AddWithValue("@id", clientId);
                await updateClientCmd.ExecuteNonQueryAsync();

                // 새로운 사용 상태를 로그에 기록합니다.
                var insertLogCmd = new SqliteCommand("INSERT INTO UsageLogs (ClientId, Timestamp, IsCatiaRunning) VALUES (@id, @time, @status)", connection);
                insertLogCmd.Parameters.AddWithValue("@id", clientId);
                insertLogCmd.Parameters.AddWithValue("@time", currentTime);
                insertLogCmd.Parameters.AddWithValue("@status", isCatiaRunning ? 1 : 0); // bool을 integer로 변환

                await insertLogCmd.ExecuteNonQueryAsync();
            }
        }

        /// <summary>
        /// (신규) 웹 대시보드를 위해 모든 클라이언트의 최신 상태를 조회합니다.
        /// Gets the latest status summary for all clients for the web dashboard.
        /// </summary>
        /// <returns>A list of client status summaries. 클라이언트 상태 요약 정보 리스트를 반환합니다.</returns>
        public async Task<List<ClientStatusSummary>> GetClientStatusSummaryAsync()
        {
            var summaryList = new List<ClientStatusSummary>();
            using (var connection = new SqliteConnection(_connectionString))
            {
                await connection.OpenAsync();
                // 각 ClientId 별로 가장 최근의 LogId를 찾는 서브쿼리를 사용하여
                // 클라이언트 정보와 최신 로그를 조합(LEFT JOIN)합니다.
                var commandText = @"
                    SELECT 
                        c.ClientId, 
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
                        summaryList.Add(new ClientStatusSummary
                        {
                            ClientId = reader.GetInt32(0),
                            IpAddress = reader.GetString(1),
                            LastSeen = reader.GetString(2),
                            // 로그가 하나도 없는 클라이언트의 경우를 대비하여 Null 체크를 합니다.
                            IsCatiaRunning = !reader.IsDBNull(3) && reader.GetInt32(3) == 1,
                            LastLogTime = !reader.IsDBNull(4) ? reader.GetString(4) : "N/A"
                        });
                    }
                }
            }
            return summaryList;
        }
    }
}
