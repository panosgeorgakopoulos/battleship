# Battleship (WinForms)

A classic Battleship game clone written in C# (Windows Forms). You play against a basic computer opponent. It tracks wins/losses in a local SQLite database.

## Tech Stack

* **Language:** C# (.NET Framework 4.7.2)
* **GUI:** Windows Forms (WinForms)
* **Database:** SQLite (via `System.Data.SQLite`)
* **ORM:** Entity Framework 6 (referenced, but mostly raw SQL used)

## Features

* **Gameplay:** 10x10 grid. You place your ships manually (Carrier, Battleship, Cruiser, Destroyer).
* **AI:** The computer places ships randomly and fires back after your turn.
* **Stats Tracking:** Saves game results (Winner, Duration, Timestamp) to `battleship_stats.db`.
* **Sound Effects:** Simple wav playback for hits, misses, and sinking ships.

## Setup

1.  **Open in Visual Studio:**
    Open `battleship.sln`.

2.  **Restore NuGet Packages:**
    The project relies on `System.Data.SQLite` and `EntityFramework`. Right-click the solution -> **Restore NuGet Packages** to pull them down.

3.  **Fix Sound Paths:**
    ⚠️ **Important:** The sound paths are currently hardcoded in `Form1.cs` inside the `SoundManager` class:
    ```csharp
    // Current code (Line ~450):
    hit = new SoundPlayer("C:\\Users\\panos\\source\\repos\\battleship\\battleship\\Resources\\hit.wav");
    ```
    You need to change these to relative paths or update them to match your local folder structure, otherwise the app will crash when sound tries to play.

4.  **Run:**
    Hit `F5` to build and run.

## How to Play

1.  **Placement Phase:**
    * Select a ship from the dropdown.
    * Click on your grid (Left panel).
    * Choose orientation (Horizontal/Vertical) in the popup.
    * Repeat until all ships are placed.

2.  **Battle Phase:**
    * Click on the Computer's grid (Right panel) to fire.
    * **Red** = Hit, **Gray** = Miss.
    * The computer fires back immediately.

3.  **Game Over:**
    * First to sink all enemy ships wins.
    * Enter your name to save your score to the database.

## Notes / Known Issues

* **Sound Paths:** As mentioned above, `SoundManager` uses absolute paths. Needs refactoring to use project resources properly.
* **Database:** If the DB doesn't exist, the app creates `battleship_stats.db` automatically on startup.

## License

None
