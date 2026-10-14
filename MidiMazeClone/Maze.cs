using System.IO;

namespace MidiMazeClone
{
    /// <summary>
    /// The maze is a grid of cells, each either wall or floor. One cell is 1.0 world units.
    /// Cell (x, y) covers world coordinates [x, x+1) x [y, y+1). Anything outside counts as wall.
    /// </summary>
    public sealed class Maze
    {
        public readonly int Width;
        public readonly int Height;

        private readonly bool[,] _walls;   // [x, y]

        public Maze(int width, int height)
        {
            Width = width;
            Height = height;
            _walls = new bool[width, height];
        }

        public bool IsWall(int x, int y)
        {
            if (x < 0 || y < 0 || x >= Width || y >= Height)
            {
                return true;
            }
            return _walls[x, y];
        }

        public void SetWall(int x, int y, bool isWall)
        {
            if (x < 0 || y < 0 || x >= Width || y >= Height)
            {
                return;
            }
            _walls[x, y] = isWall;
        }

        /// <summary>
        /// Parse the text format: '#' is wall, any other character is floor.
        /// Empty lines and lines starting with ';' are ignored. Short lines are padded with wall.
        /// </summary>
        public static Maze Parse(IList<string> lines)
        {
            List<string> rows = new List<string>();
            int width = 0;

            for (int i = 0; i < lines.Count; i++)
            {
                string line = lines[i].TrimEnd('\r', '\n');
                if (line.Length == 0)
                {
                    continue;
                }
                if (line[0] == ';')
                {
                    continue;
                }

                rows.Add(line);
                if (line.Length > width)
                {
                    width = line.Length;
                }
            }

            if (rows.Count == 0 || width == 0)
            {
                throw new InvalidDataException("Maze has no rows.");
            }

            Maze maze = new Maze(width, rows.Count);
            for (int y = 0; y < rows.Count; y++)
            {
                string row = rows[y];
                for (int x = 0; x < width; x++)
                {
                    bool isWall = true;
                    if (x < row.Length)
                    {
                        isWall = row[x] == '#';
                    }
                    maze._walls[x, y] = isWall;
                }
            }

            return maze;
        }

        public static Maze LoadFromFile(string path)
        {
            string[] lines = File.ReadAllLines(path);
            return Parse(lines);
        }

        /// <summary>Small fallback maze, used when no maze file can be found.</summary>
        public static Maze CreateBuiltIn()
        {
            string[] lines =
            {
                "################",
                "#......#.......#",
                "#.####.#.#####.#",
                "#.#....#.....#.#",
                "#.#.#######.##.#",
                "#.#.#.....#....#",
                "#...#.###.####.#",
                "#.###.#.#......#",
                "#.....#.######.#",
                "################",
            };
            return Parse(lines);
        }

        public int CountFreeCells()
        {
            int count = 0;
            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    if (!_walls[x, y])
                    {
                        count++;
                    }
                }
            }
            return count;
        }

        /// <summary>
        /// Flood fill from a start cell. If this returns less than CountFreeCells(),
        /// the maze has unreachable pockets where a player could spawn and be stuck.
        /// </summary>
        public int CountReachableFrom(int startX, int startY)
        {
            if (IsWall(startX, startY))
            {
                return 0;
            }

            bool[,] visited = new bool[Width, Height];
            Stack<(int, int)> open = new Stack<(int, int)>();
            open.Push((startX, startY));
            visited[startX, startY] = true;
            int count = 0;

            while (open.Count > 0)
            {
                (int x, int y) = open.Pop();
                count++;

                TryVisit(x + 1, y, visited, open);
                TryVisit(x - 1, y, visited, open);
                TryVisit(x, y + 1, visited, open);
                TryVisit(x, y - 1, visited, open);
            }

            return count;
        }

        private void TryVisit(int x, int y, bool[,] visited, Stack<(int, int)> open)
        {
            if (IsWall(x, y))
            {
                return;
            }
            if (visited[x, y])
            {
                return;
            }
            visited[x, y] = true;
            open.Push((x, y));
        }
    }
}
