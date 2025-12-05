using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Media;
using System.Windows.Forms;

namespace battleship
{
    public partial class Form1 : Form
    {
        private Button[,] playerGridButtons = new Button[10, 10];
        private Button[,] computerGridButtons = new Button[10, 10];
        private bool[,] playerShips = new bool[10, 10];
        private bool[,] computerShips = new bool[10, 10];
        private Dictionary<string, List<Point>> playerShipCoordinates = new Dictionary<string, List<Point>>();
        private Dictionary<string, List<Point>> computerShipCoordinates = new Dictionary<string, List<Point>>();
        private HashSet<Point> playerMoves = new HashSet<Point>();
        private Random random = new Random();
        private int computerShipCount = 14;
        private bool gameStarted = false;
        private int playerAttempts = 0;
        private Stopwatch gameTimer = new Stopwatch();
        private SoundManager soundManager = new SoundManager();

        private readonly (string Name, int Size)[] shipTypes = new (string, int)[]
        {
            ("Carrier", 5),
            ("Battleship", 4),
            ("Cruiser", 3),
            ("Destroyer", 2)
        };

        private ComboBox shipSelectionComboBox;
        private int remainingShipsToPlace;
        private int playerWins = 0;
        private int computerWins = 0;

        // Database Path
        private const string DatabasePath = "battleship_stats.db";

        private GameManager gameManager;

        public Form1()
        {
            InitializeComponent();
            InitializeDatabase();
            InitializeGame();
            CenterTimerLabel();

            // Initialize GameManager
            gameManager = new GameManager(playerShips, computerShips, playerShipCoordinates, computerShipCoordinates, computerShipCount);
        }

        private void InitializeDatabase()
        {
            if (!File.Exists(DatabasePath))
            {
                SQLiteConnection.CreateFile(DatabasePath);

                using (var connection = new SQLiteConnection($"Data Source={DatabasePath};Version=3;"))
                {
                    connection.Open();
                    string createTableQuery = @"
                        CREATE TABLE GameStats (
                            ID INTEGER PRIMARY KEY AUTOINCREMENT,
                            PlayerName TEXT NOT NULL,
                            Winner TEXT NOT NULL,
                            Duration TEXT NOT NULL,
                            Timestamp DATETIME DEFAULT CURRENT_TIMESTAMP
                        )";
                    using (var command = new SQLiteCommand(createTableQuery, connection))
                    {
                        command.ExecuteNonQuery();
                    }
                }
            }
        }

        private void SaveGameStats(string playerName, string winner, string duration)
        {
            using (var connection = new SQLiteConnection($"Data Source={DatabasePath};Version=3;"))
            {
                connection.Open();
                string insertQuery = "INSERT INTO GameStats (PlayerName, Winner, Duration) VALUES (@PlayerName, @Winner, @Duration)";
                using (var command = new SQLiteCommand(insertQuery, connection))
                {
                    command.Parameters.AddWithValue("@PlayerName", playerName);
                    command.Parameters.AddWithValue("@Winner", winner);
                    command.Parameters.AddWithValue("@Duration", duration);
                    command.ExecuteNonQuery();
                }
            }
        }

        private void InitializeGame()
        {
            CreateGrid(PlayerGrid, playerGridButtons, true);
            CreateGrid(ComputerGrid, computerGridButtons, false);
            InitializeShipComboBox();
            PlaceComputerShips();
            remainingShipsToPlace = shipTypes.Length;
            playerAttempts = 0;
            gameTimer.Reset();
        }

        private void PlayerTimer_Tick(object sender, EventArgs e)
        {
            TimeSpan elapsed = gameTimer.Elapsed;
            playerTimeLabel.Text = $"Time: {elapsed.Minutes:D2}:{elapsed.Seconds:D2}";
        }

        private void CenterTimerLabel()
        {
            int referenceY = label43.Location.Y + label43.Height + 20;
            playerTimeLabel.Location = new Point(label43.Location.X, referenceY);
            playerTimeLabel.TextAlign = ContentAlignment.MiddleCenter;
        }

        private void CreateGrid(Panel gridPanel, Button[,] gridButtons, bool isPlayer)
        {
            gridPanel.Controls.Clear();

            int buttonWidth = gridPanel.Width / 10;
            int buttonHeight = gridPanel.Height / 10;

            for (int row = 0; row < 10; row++)
            {
                for (int col = 0; col < 10; col++)
                {
                    Button button = new Button
                    {
                        Width = buttonWidth,
                        Height = buttonHeight,
                        BackColor = Color.LightGray,
                        Location = new Point(col * buttonWidth, row * buttonHeight),
                        Tag = new Point(row, col)
                    };

                    if (isPlayer)
                        button.Click += PlayerGrid_Click;
                    else
                        button.Click += ComputerGrid_Click;

                    gridPanel.Controls.Add(button);
                    gridButtons[row, col] = button;
                }
            }
        }

        private void InitializeShipComboBox()
        {
            shipSelectionComboBox = new ComboBox
            {
                Location = new Point(330, 85),
                Width = 150
            };

            foreach (var ship in shipTypes)
            {
                shipSelectionComboBox.Items.Add(ship.Name);
            }

            shipSelectionComboBox.SelectedIndex = 0;
            Controls.Add(shipSelectionComboBox);
        }

        private void PlaceComputerShips()
        {
            foreach (var ship in shipTypes)
            {
                PlaceShip(computerShips, ship.Size, computerShipCoordinates, ship.Name);
            }
        }

        private void PlayerGrid_Click(object sender, EventArgs e)
        {
            if (gameStarted) return;

            Button button = sender as Button;
            Point position = (Point)button.Tag;
            int row = position.X;
            int col = position.Y;

            string selectedShip = shipSelectionComboBox.SelectedItem.ToString();
            var currentShip = shipTypes.First(s => s.Name == selectedShip);

            bool horizontal = MessageBox.Show("Τοποθέτηση οριζόντια?", "Orientation", MessageBoxButtons.YesNo) == DialogResult.Yes;

            if (CanPlaceShip(row, col, currentShip.Size, horizontal, playerShips))
            {
                List<Point> points = new List<Point>();
                for (int i = 0; i < currentShip.Size; i++)
                {
                    int r = row + (horizontal ? 0 : i);
                    int c = col + (horizontal ? i : 0);
                    playerShips[r, c] = true;
                    playerGridButtons[r, c].BackColor = Color.Blue;
                    points.Add(new Point(r, c));
                }
                playerShipCoordinates[selectedShip] = points;

                shipSelectionComboBox.Items.Remove(selectedShip);
                shipSelectionComboBox.SelectedIndex = shipSelectionComboBox.Items.Count > 0 ? 0 : -1;
                remainingShipsToPlace--;

                if (remainingShipsToPlace == 0)
                {
                    MessageBox.Show("Όλα τα πλοία τοποθετήθηκαν! Το παιχνίδι ξεκινάει. ");
                    StartGame();
                }
            }
            else
            {
                MessageBox.Show("Δεν μπορεί να τοποθετηθεί εκεί πλοίο.");
            }

            if (computerShips[row, col])
            {
                button.BackColor = Color.Red;
                computerShips[row, col] = false;
                soundManager.PlayHitSound();
            }
            else
            {
                button.BackColor = Color.Gray;
                soundManager.PlayMissSound();
            }
        
        }

        private void ComputerGrid_Click(object sender, EventArgs e)
        {
            if (!gameStarted) return;

            Button button = sender as Button;
            Point position = (Point)button.Tag;
            int row = position.X;
            int col = position.Y;

            if (playerMoves.Contains(position))
            {
                MessageBox.Show("Έχεις επιλέξει ήδη αυτό το κουτάκι. Διάλεξε κάποιο άλλο.");
                return;
            }

            playerMoves.Add(position);
            playerAttempts++;

            if (computerShips[row, col])
            {
                button.BackColor = Color.Red;
                computerShips[row, col] = false;
                CheckAndAnnounceShipSunk(computerShipCoordinates, row, col, "Μου βύθισες το {0}!");
                computerShipCount--;
                if (computerShipCount == 0)
                {
                    playerWins++;
                    EndGame(true);
                }
            }
            else
            {
                button.BackColor = Color.Gray;
                gameManager.ComputerTurn(playerGridButtons);
            }
        }

        private bool CanPlaceShip(int row, int col, int size, bool horizontal, bool[,] grid)
        {
            if (horizontal && col + size > 10) return false;
            if (!horizontal && row + size > 10) return false;

            for (int i = 0; i < size; i++)
            {
                int r = row + (horizontal ? 0 : i);
                int c = col + (horizontal ? i : 0);
                if (grid[r, c]) return false;
            }

            return true;
        }

        private void PlaceShip(bool[,] grid, int size, Dictionary<string, List<Point>> coordinates, string shipName)
        {
            while (true)
            {
                int row = random.Next(10);
                int col = random.Next(10);
                bool horizontal = random.Next(2) == 0;

                if (CanPlaceShip(row, col, size, horizontal, grid))
                {
                    List<Point> points = new List<Point>();
                    for (int i = 0; i < size; i++)
                    {
                        int r = row + (horizontal ? 0 : i);
                        int c = col + (horizontal ? i : 0);
                        grid[r, c] = true;
                        points.Add(new Point(r, c));
                    }
                    coordinates[shipName] = points;
                    break;
                }
            }
        }

        private void CheckAndAnnounceShipSunk(Dictionary<string, List<Point>> shipCoordinates, int row, int col, string message)
        {
            foreach (var kvp in shipCoordinates)
            {
                if (kvp.Value.Contains(new Point(row, col)))
                {
                    kvp.Value.Remove(new Point(row, col));
                    if (kvp.Value.Count == 0)
                    {
                        soundManager.PlaySinkSound();
                        MessageBox.Show(string.Format(message, kvp.Key));
                        shipCoordinates.Remove(kvp.Key);
                    }
                    break;
                }
            }
        }

        private void ResetGame()
        {
            gameStarted = false;
            remainingShipsToPlace = shipTypes.Length;
            computerShipCount = shipTypes.Sum(ship => ship.Size);

            playerShips = new bool[10, 10];
            computerShips = new bool[10, 10];
            playerShipCoordinates.Clear();
            computerShipCoordinates.Clear();
            playerMoves.Clear();
            playerAttempts = 0;
            gameTimer.Reset();

            Controls.Remove(shipSelectionComboBox);

            InitializeGame();
        }

        private void StartGame()
        {
            gameStarted = true;
            gameTimer.Start();
            PlayerTimer.Start();
        }

        private void EndGame(bool playerWon)
        {
            gameTimer.Stop();
            PlayerTimer.Stop();

            string winner = playerWon ? "Player" : "Computer";
            string duration = $"{gameTimer.Elapsed.Minutes:D2}:{gameTimer.Elapsed.Seconds:D2}";

            string playerName = PromptPlayerName();
            SaveGameStats(playerName, winner, duration);

            string message = playerWon
                ? $"Συγχαρητήρια {playerName}! Κέρδισες σε {playerAttempts} προσπάθειες και {duration}."
                : $"Το computer κέρδισε. Καλή τύχη την επόμενη φορά!\nΔιάρκεια παιχνδιού: {duration}.";

            var result = MessageBox.Show($"{message}\nPlayer Wins: {playerWins}, Computer Wins: {computerWins}\nΘες να ξαναπαίξεις?",
                                         "Game Over", MessageBoxButtons.YesNo);

            if (result == DialogResult.Yes)
            {
                ResetGame();
            }
            else
            {
                Application.Exit();
            }
        }

        private string PromptPlayerName()
        {
            string playerName = "Player";
            using (var inputDialog = new Form())
            {
                inputDialog.Width = 400;
                inputDialog.Height = 150;
                inputDialog.Text = "Enter Your Name";

                Label textLabel = new Label() { Left = 50, Top = 20, Text = "Player Name:" };
                TextBox inputBox = new TextBox() { Left = 150, Top = 20, Width = 200 };
                Button confirmation = new Button() { Text = "OK", Left = 250, Width = 100, Top = 50 };

                confirmation.Click += (sender, e) => { inputDialog.DialogResult = DialogResult.OK; inputDialog.Close(); };
                inputDialog.Controls.Add(textLabel);
                inputDialog.Controls.Add(inputBox);
                inputDialog.Controls.Add(confirmation);
                inputDialog.AcceptButton = confirmation;

                if (inputDialog.ShowDialog() == DialogResult.OK)
                {
                    playerName = inputBox.Text.Trim();
                }
            }
            return string.IsNullOrEmpty(playerName) ? "Player" : playerName;
        }

        private void viewStatsButton_Click(object sender, EventArgs e)
        {
            // Open the database and fetch stats
            string dbPath = "battleship_stats.db";
            var stats = new List<(int ID, string PlayerName, string Winner, string Duration, string Timestamp)>();

            using (var connection = new SQLiteConnection($"Data Source={dbPath};Version=3;"))
            {
                connection.Open();
                string query = "SELECT * FROM GameStats";

                using (var command = new SQLiteCommand(query, connection))
                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        stats.Add((
                            ID: reader.GetInt32(0),
                            PlayerName: reader.GetString(1),
                            Winner: reader.GetString(2),
                            Duration: reader.GetString(3),
                            Timestamp: reader.GetString(4)
                        ));
                    }
                }
            }

            // Create a new form to display stats
            Form statsForm = new Form
            {
                Text = "Game Statistics",
                Width = 600,
                Height = 400
            };

            // Add a DataGridView to display stats
            DataGridView statsGrid = new DataGridView
            {
                Dock = DockStyle.Fill,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
            };
            statsGrid.DataSource = stats.Select(s => new
            {
                ID = s.ID,
                PlayerName = s.PlayerName,
                Winner = s.Winner,
                Duration = s.Duration,
                Timestamp = s.Timestamp
            }).ToList();

            statsForm.Controls.Add(statsGrid);
            statsForm.ShowDialog();
        }
    }
        public class GameManager
        {
            private Random random = new Random();
            private HashSet<Point> computerMoves = new HashSet<Point>();
            private bool[,] playerShips;
            private bool[,] computerShips;
            private Dictionary<string, List<Point>> playerShipCoordinates;
            private Dictionary<string, List<Point>> computerShipCoordinates;
            private int computerShipCount;

            public GameManager(bool[,] playerShips, bool[,] computerShips, Dictionary<string, List<Point>> playerShipCoordinates, Dictionary<string, List<Point>> computerShipCoordinates, int computerShipCount)
            {
                this.playerShips = playerShips;
                this.computerShips = computerShips;
                this.playerShipCoordinates = playerShipCoordinates;
                this.computerShipCoordinates = computerShipCoordinates;
                this.computerShipCount = computerShipCount;
            }

            public void ComputerTurn(Button[,] playerGridButtons)
            {
                while (true)
                {
                    int row = random.Next(10);
                    int col = random.Next(10);
                    Point move = new Point(row, col);

                    if (computerMoves.Contains(move)) continue;

                    computerMoves.Add(move);

                    if (playerShips[row, col])
                    {
                        playerGridButtons[row, col].BackColor = Color.Red;
                        playerShips[row, col] = false;
                        CheckAndAnnounceShipSunk(playerShipCoordinates, row, col, "Βυθίστηκε το {0} μου!");
                        if (playerShips.Cast<bool>().Count(v => v) == 0){//End game
                         }
                        break;
                    }
                    else if (playerGridButtons[row, col].BackColor == Color.LightGray)
                    {
                        playerGridButtons[row, col].BackColor = Color.Gray;
                        break;
                    }
                }
            }

            private void CheckAndAnnounceShipSunk(Dictionary<string, List<Point>> shipCoordinates, int row, int col, string message)
            {
                foreach (var kvp in shipCoordinates)
                {
                    if (kvp.Value.Contains(new Point(row, col)))
                    {
                        kvp.Value.Remove(new Point(row, col));
                        if (kvp.Value.Count == 0)
                        {
                            MessageBox.Show(string.Format(message, kvp.Key));
                            shipCoordinates.Remove(kvp.Key);
                        }
                        break;
                    }
                }
            }
    }

        public class SoundManager
        {
            private SoundPlayer hit;
            private SoundPlayer miss;
            private SoundPlayer sink;
            

            public SoundManager()
            {
                // Load sound files
                hit = new SoundPlayer("C:\\Users\\panos\\source\\repos\\battleship\\battleship\\Resources\\hit.wav");
                miss = new SoundPlayer("C:\\Users\\panos\\source\\repos\\battleship\\battleship\\Resources\\miss.wav");
                sink = new SoundPlayer("C:\\Users\\panos\\source\\repos\\battleship\\battleship\\Resources\\sink.wav");
            }

            public void PlayHitSound()
            {
                hit.Play();
            }

            public void PlayMissSound()
            {
                miss.Play();
            }

            public void PlaySinkSound()
            {
                sink.Play();
            }
        }
    }