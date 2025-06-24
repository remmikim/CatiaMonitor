using Microsoft.Data.Sqlite;
using System;
using System.IO;
using System.Threading.Tasks;

namespace CatiaMonitor.Server
{
    /// <summary>
    /// Manages all database operations using SQLite.
    /// SQLite를 사용하여 모든 데이터베이스 작업을 관리합니다.
    /// This class must be public to be accessible by ClientHandler.
    /// ClientHandler에서 접근할 수 있도록 이 클래스는 public이어야 합니다.
    /// </summary>
    public class DatabaseManager
    {
        private readonly string _connectionString;

        /// <summary>
        /// Initializes a new instance of the DatabaseManager class.
        /// DatabaseManager 클래스의 새 인스턴스를 초기화합니다.
        /// </summary>
        /// <param name="dbFileName">The name of the SQLite database file. SQLite 데이터베이스 파일의 이름입니다.</param>
        public DatabaseManager(string dbFileName = "catia_monitor.db")
        {
            // 데이터베이스 파일이 프로그램 실행 폴더에 생성되도록 경로를 설정합니다.
            string dbPath = Path.Combine(AppContext.BaseDirectory, dbFileName);
            _connectionString = $"Data Source={dbPath}";
        }

        /// <summary>
        /// Initializes the database by creating necessary tables if they don't exist.
        /// 필요한 테이블이 없는 경우 생성하여 데이터베이스를 초기화합니다.
        /// </summary>
        public async Task InitializeDatabaseAsync()
        {
            using (var connection = new SqliteConnection(_connectionString))
            {
                await connection.OpenAsync();

                // Clients 테이블 생성 SQL
                var createClientsTable = @"
                    CREATE TABLE IF NOT EXISTS Clients (
                        ClientId    INTEGER PRIMARY KEY AUTOINCREMENT,
                        IpAddress   TEXT NOT NULL UNIQUE,
                        LastSeen    TEXT NOT NULL
                    );";

                using (var command = new SqliteCommand(createClientsTable, connection))
                {
                    await command.ExecuteNonQueryAsync();
                }

                // UsageLogs 테이블 생성 SQL
                var createUsageLogsTable = @"
                    CREATE TABLE IF NOT EXISTS UsageLogs (
                        LogId           INTEGER PRIMARY KEY AUTOINCREMENT,
                        ClientId        INTEGER NOT NULL,
                        Timestamp       TEXT NOT NULL,
                        IsCatiaRunning  INTEGER NOT NULL,
                        FOREIGN KEY(ClientId) REFERENCES Clients(ClientId)
                    );";

                using (var command = new SqliteCommand(createUsageLogsTable, connection))
                {
                    await command.ExecuteNonQueryAsync();
                }
            }
            Console.WriteLine("[Database] Database initialized successfully.");
        }

        /// <summary>
        /// Ensures a client exists in the database. If not, creates a new entry. Updates LastSeen time.
        /// 클라이언트가 데이터베이스에 존재하는지 확인합니다. 없으면 새 항목을 만들고, 있으면 LastSeen 시간을 업데이트합니다.
        /// </summary>
        /// <param name="ipAddress">The IP address of the client. 클라이언트의 IP 주소입니다.</param>
        /// <returns>The ID of the client. 클라이언트의 ID를 반환합니다.</returns>
        public async Task<int> EnsureClientExists(string ipAddress)
        {
            using (var connection = new SqliteConnection(_connectionString))
            {
                await connection.OpenAsync();

                // 먼저 해당 IP의 클라이언트가 있는지 확인합니다.
                var selectCmd = new SqliteCommand("SELECT ClientId FROM Clients WHERE IpAddress = @ip", connection);
                selectCmd.Parameters.AddWithValue("@ip", ipAddress);

                var clientId = await selectCmd.ExecuteScalarAsync();

                if (clientId != null)
                {
                    // 클라이언트가 존재하면 LastSeen 시간만 업데이트합니다.
                    var updateCmd = new SqliteCommand("UPDATE Clients SET LastSeen = @time WHERE ClientId = @id", connection);
                    updateCmd.Parameters.AddWithValue("@time", DateTime.UtcNow.ToString("o")); // ISO 8601 format
                    updateCmd.Parameters.AddWithValue("@id", Convert.ToInt32(clientId));
                    await updateCmd.ExecuteNonQueryAsync();
                    return Convert.ToInt32(clientId);
                }
                else
                {
                    // 클라이언트가 없으면 새로 추가합니다.
                    var insertCmd = new SqliteCommand("INSERT INTO Clients (IpAddress, LastSeen) VALUES (@ip, @time); SELECT last_insert_rowid();", connection);
                    insertCmd.Parameters.AddWithValue("@ip", ipAddress);
                    insertCmd.Parameters.AddWithValue("@time", DateTime.UtcNow.ToString("o"));

                    // 새로 추가된 행의 ID를 반환받습니다.
                    var newClientId = await insertCmd.ExecuteScalarAsync();
                    return Convert.ToInt32(newClientId);
                }
            }
        }

        /// <summary>
        /// Logs the CATIA usage status for a specific client.
        /// 특정 클라이언트의 CATIA 사용 상태를 기록합니다.
        /// </summary>
        /// <param name="clientId">The ID of the client. 클라이언트의 ID입니다.</param>
        /// <param name="isCatiaRunning">The running status of CATIA. CATIA의 실행 상태입니다.</param>
        public async Task LogUsage(int clientId, bool isCatiaRunning)
        {
            using (var connection = new SqliteConnection(_connectionString))
            {
                await connection.OpenAsync();

                var insertCmd = new SqliteCommand("INSERT INTO UsageLogs (ClientId, Timestamp, IsCatiaRunning) VALUES (@id, @time, @status)", connection);
                insertCmd.Parameters.AddWithValue("@id", clientId);
                insertCmd.Parameters.AddWithValue("@time", DateTime.UtcNow.ToString("o"));
                insertCmd.Parameters.AddWithValue("@status", isCatiaRunning ? 1 : 0); // bool을 integer로 변환 (True=1, False=0)

                await insertCmd.ExecuteNonQueryAsync();
            }
        }
    }
}
