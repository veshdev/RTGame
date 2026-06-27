using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Server.Network;

namespace Server.World;

public static class MapData
{
    public static int TileIndex(int width, int tx, int ty) => ty * width + tx;

    public static TileType GetTile(byte[] tiles, int width, int height, int tx, int ty)
    {
        if (tiles == null || tx < 0 || ty < 0 || tx >= width || ty >= height)
            return TileType.Wall;
        return (TileType)tiles[TileIndex(width, tx, ty)];
    }

    public static void SetTile(byte[] tiles, int width, int height, int tx, int ty, TileType tileType)
    {
        if (tiles == null || tx < 0 || ty < 0 || tx >= width || ty >= height)
            return;
        tiles[TileIndex(width, tx, ty)] = (byte)tileType;
    }

    public static (int tx, int ty) WorldToTile(float worldX, float worldY)
    {
        return ((int)(worldX / DataSizes.TileSize), (int)(worldY / DataSizes.TileSize));
    }

    public static (float x, float y) TileToWorldCenter(int tx, int ty)
    {
        return (tx * DataSizes.TileSize + DataSizes.TileSize / 2.0f,
                ty * DataSizes.TileSize + DataSizes.TileSize / 2.0f);
    }

    public static bool IsPassable(byte[] tiles, int width, int height, int tx, int ty)
    {
        return GetTile(tiles, width, height, tx, ty) is
            TileType.Road or TileType.Floor or TileType.Spawn or TileType.Extraction or TileType.Door;
    }

    public static bool BlocksMovement(byte[] tiles, int width, int height, int tx, int ty)
    {
        return !IsPassable(tiles, width, height, tx, ty);
    }

    public static byte[] SerializeMap(byte[] tiles, int width, int height, uint mapHash)
    {
        int tileBytes = width * height;
        byte[] result = new byte[8 + tileBytes];
        Span<byte> span = result.AsSpan();
        BinaryPrimitives.WriteUInt32LittleEndian(span[0..4], mapHash);
        BinaryPrimitives.WriteUInt16LittleEndian(span[4..6], (ushort)width);
        BinaryPrimitives.WriteUInt16LittleEndian(span[6..8], (ushort)height);
        Buffer.BlockCopy(tiles, 0, result, 8, tileBytes);
        return result;
    }
}

internal sealed class LoadedMap
{
    public required byte[] Tiles { get; init; }
    public int Width { get; init; }
    public int Height { get; init; }
    public List<(float x, float y)> PlayerSpawns { get; init; } = new();
    public List<ExtractionZone> ExtractionZones { get; init; } = new();
    public uint MapHash { get; init; }
}

internal readonly struct MapValidationResult
{
    public bool Ok { get; init; }
    public string Error { get; init; }

    public static MapValidationResult Success() => new() { Ok = true, Error = string.Empty };
    public static MapValidationResult Failure(string error) => new() { Ok = false, Error = error };
}

internal static class MapLoader
{
    private const float ExtractionRadius = 56.0f;
    private const int MonsterSpawnBufferRadius = 5;

    private static readonly Dictionary<char, TileType> CharToTile = new()
    {
        ['R'] = TileType.Road,
        ['F'] = TileType.Floor,
        ['W'] = TileType.Wall,
        ['D'] = TileType.Door,
        ['S'] = TileType.Spawn,
        ['E'] = TileType.Extraction,
    };

    public static MapValidationResult Validate(string path)
    {
        try
        {
            Load(path);
            return MapValidationResult.Success();
        }
        catch (Exception ex)
        {
            return MapValidationResult.Failure(ex.Message);
        }
    }

    public static LoadedMap Load(string path)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException($"Map file not found: {path}");

        string[] rawLines = ReadAllLines(path);
        var rows = new List<(int SourceLine, string Tiles)>();
        for (int i = 0; i < rawLines.Length; i++)
        {
            string? row = ParseTileRow(rawLines[i]);
            if (row == null)
                continue;
            rows.Add((i + 1, row));
        }

        if (rows.Count == 0)
            throw new InvalidDataException($"Map '{Path.GetFileName(path)}' contains no tile rows");

        int height = rows.Count;
        int width = rows[0].Tiles.Length;
        if (width == 0)
            throw new InvalidDataException($"Map '{Path.GetFileName(path)}' row 1 (line {rows[0].SourceLine}) is empty");
        if (width > DataSizes.MaxMapW)
            throw new InvalidDataException($"Map '{Path.GetFileName(path)}' width {width} exceeds maximum {DataSizes.MaxMapW}");
        if (height > DataSizes.MaxMapH)
            throw new InvalidDataException($"Map '{Path.GetFileName(path)}' height {height} exceeds maximum {DataSizes.MaxMapH}");

        for (int y = 1; y < height; y++)
        {
            int rowWidth = rows[y].Tiles.Length;
            if (rowWidth != width)
            {
                throw new InvalidDataException(
                    $"Map '{Path.GetFileName(path)}' row {y + 1} (line {rows[y].SourceLine}) has width {rowWidth}, expected {width} (from row 1 / line {rows[0].SourceLine})");
            }
        }

        byte[] tiles = new byte[width * height];
        var spawns = new List<(float x, float y)>();
        var extractions = new List<(float x, float y)>();

        for (int y = 0; y < height; y++)
        {
            string row = rows[y].Tiles;
            for (int x = 0; x < width; x++)
            {
                char c = row[x];
                if (!CharToTile.TryGetValue(c, out TileType tile))
                {
                    throw new InvalidDataException(
                        $"Map '{Path.GetFileName(path)}' has unknown tile '{c}' at column {x + 1}, row {y + 1} (line {rows[y].SourceLine})");
                }

                tiles[y * width + x] = (byte)tile;

                if (tile == TileType.Spawn)
                    spawns.Add(MapData.TileToWorldCenter(x, y));
                else if (tile == TileType.Extraction)
                    extractions.Add(MapData.TileToWorldCenter(x, y));
            }
        }

        if (spawns.Count == 0)
            throw new InvalidDataException($"Map '{Path.GetFileName(path)}' must contain at least one spawn tile (S)");

        return new LoadedMap
        {
            Tiles = tiles,
            Width = width,
            Height = height,
            PlayerSpawns = spawns,
            ExtractionZones = extractions.Select(e => new ExtractionZone(e.x, e.y, ExtractionRadius)).ToList(),
            MapHash = ComputeHash(path, tiles),
        };
    }

    private static string[] ReadAllLines(string path)
    {
        byte[] bytes = File.ReadAllBytes(path);
        string text;
        if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE)
            text = Encoding.Unicode.GetString(bytes);
        else if (bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF)
            text = Encoding.BigEndianUnicode.GetString(bytes);
        else
            text = Encoding.UTF8.GetString(bytes);

        return text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
    }

    private static string? ParseTileRow(string rawLine)
    {
        string line = rawLine.TrimEnd('\r', '\n', '\0');
        // Remove BOM character if present at the start
        if (line.Length > 0 && line[0] == '\uFEFF')
            line = line[1..];

        int comment = line.IndexOf('#');
        if (comment >= 0)
            line = line[..comment];

        var sb = new StringBuilder(line.Length);
        foreach (char c in line)
        {
            if (c == '\0' || char.IsWhiteSpace(c))
                continue;
            sb.Append(c);
        }

        return sb.Length == 0 ? null : sb.ToString();
    }

    public static List<(float x, float y)> PickMonsterSpawns(
        byte[] tiles, int width, int height,
        List<(float x, float y)> playerSpawns, int count, uint seed)
    {
        var rng = new Random((int)seed);
        var candidates = new List<(float x, float y)>();
        var playerTiles = new HashSet<(int, int)>();

        foreach (var (wx, wy) in playerSpawns)
        {
            int ptx = (int)(wx / DataSizes.TileSize);
            int pty = (int)(wy / DataSizes.TileSize);
            for (int dy = -MonsterSpawnBufferRadius; dy <= MonsterSpawnBufferRadius; dy++)
            {
                for (int dx = -MonsterSpawnBufferRadius; dx <= MonsterSpawnBufferRadius; dx++)
                    playerTiles.Add((ptx + dx, pty + dy));
            }
        }

        for (int ty = 0; ty < height; ty++)
        {
            for (int tx = 0; tx < width; tx++)
            {
                if (playerTiles.Contains((tx, ty)))
                    continue;
                TileType tile = MapData.GetTile(tiles, width, height, tx, ty);
                if (tile is not (TileType.Floor or TileType.Road or TileType.Spawn))
                    continue;
                candidates.Add(MapData.TileToWorldCenter(tx, ty));
            }
        }

        Shuffle(rng, candidates);
        return candidates.Take(count).ToList();
    }

    private static void Shuffle<T>(Random rng, List<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    private static uint ComputeHash(string path, byte[] tiles)
    {
        uint hash = 2166136261;
        foreach (char c in Path.GetFileName(path))
            hash = (hash ^ c) * 16777619;
        foreach (byte b in tiles)
            hash = (hash ^ b) * 16777619;
        return hash;
    }
}
