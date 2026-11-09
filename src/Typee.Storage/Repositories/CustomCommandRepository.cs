using Dapper;
using Typee.Core.Abstractions;
using Typee.Core.Models;
using Typee.Storage.Sqlite;

namespace Typee.Storage.Repositories;

public sealed class CustomCommandRepository : ICustomCommandRepository
{
    private readonly SqliteDbConnectionFactory _factory;

    public CustomCommandRepository(SqliteDbConnectionFactory factory)
    {
        _factory = factory;
    }

    public async Task<IReadOnlyList<CustomCommand>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = _factory.OpenConnection();
        var rows = await connection.QueryAsync<CommandRow>(
            "SELECT id, name, instruction, hotkey, use_count FROM custom_commands ORDER BY name COLLATE NOCASE;").ConfigureAwait(false);
        return rows.Select(Map).ToList();
    }

    public async Task<CustomCommand> UpsertAsync(CustomCommand command, CancellationToken cancellationToken = default)
    {
        await using var connection = _factory.OpenConnection();
        if (command.Id > 0)
        {
            await connection.ExecuteAsync(
                """
                UPDATE custom_commands
                SET name = @Name, instruction = @Instruction, hotkey = @Hotkey, use_count = @UseCount
                WHERE id = @Id;
                """,
                new
                {
                    command.Id,
                    command.Name,
                    command.Instruction,
                    command.Hotkey,
                    command.UseCount
                }).ConfigureAwait(false);
            return command;
        }

        var id = await connection.ExecuteScalarAsync<long>(
            """
            INSERT INTO custom_commands(name, instruction, hotkey, use_count)
            VALUES (@Name, @Instruction, @Hotkey, @UseCount);
            SELECT last_insert_rowid();
            """,
            new
            {
                command.Name,
                command.Instruction,
                command.Hotkey,
                command.UseCount
            }).ConfigureAwait(false);

        return new CustomCommand
        {
            Id = id,
            Name = command.Name,
            Instruction = command.Instruction,
            Hotkey = command.Hotkey,
            UseCount = command.UseCount
        };
    }

    public async Task DeleteAsync(long id, CancellationToken cancellationToken = default)
    {
        await using var connection = _factory.OpenConnection();
        await connection.ExecuteAsync("DELETE FROM custom_commands WHERE id = @Id;", new { Id = id }).ConfigureAwait(false);
    }

    public async Task ClearAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = _factory.OpenConnection();
        await connection.ExecuteAsync("DELETE FROM custom_commands;").ConfigureAwait(false);
    }

    private static CustomCommand Map(CommandRow row) => new()
    {
        Id = row.id,
        Name = row.name,
        Instruction = row.instruction,
        Hotkey = row.hotkey,
        UseCount = row.use_count
    };

    private sealed class CommandRow
    {
        public long id { get; init; }
        public string name { get; init; } = "";
        public string instruction { get; init; } = "";
        public string? hotkey { get; init; }
        public int use_count { get; init; }
    }
}
