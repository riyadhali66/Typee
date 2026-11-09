namespace Typee.Storage.Sqlite;

public static class SqliteMigrations
{
    public static IReadOnlyList<Migration> All { get; } =
    [
        new Migration(
            1,
            "initial_schema",
            """
            CREATE TABLE IF NOT EXISTS phrases (
              id INTEGER PRIMARY KEY AUTOINCREMENT,
              text TEXT NOT NULL,
              trigger_prefix TEXT NOT NULL,
              use_count INTEGER NOT NULL DEFAULT 0,
              last_used_utc TEXT NULL,
              source TEXT NOT NULL CHECK (source IN ('accepted', 'manual'))
            );
            CREATE UNIQUE INDEX IF NOT EXISTS ix_phrases_text ON phrases(text COLLATE NOCASE);

            CREATE TABLE IF NOT EXISTS completion_cache (
              context_hash TEXT PRIMARY KEY,
              suggestion TEXT NOT NULL,
              level INTEGER NOT NULL,
              created_utc TEXT NOT NULL
            );
            CREATE INDEX IF NOT EXISTS ix_completion_cache_created ON completion_cache(created_utc);

            CREATE TABLE IF NOT EXISTS user_dictionary (
              word TEXT PRIMARY KEY COLLATE NOCASE,
              added_utc TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS snippets (
              id INTEGER PRIMARY KEY AUTOINCREMENT,
              trigger TEXT NOT NULL COLLATE NOCASE,
              body TEXT NOT NULL,
              use_count INTEGER NOT NULL DEFAULT 0,
              updated_utc TEXT NOT NULL
            );
            CREATE UNIQUE INDEX IF NOT EXISTS ix_snippets_trigger ON snippets(trigger COLLATE NOCASE);

            CREATE TABLE IF NOT EXISTS custom_commands (
              id INTEGER PRIMARY KEY AUTOINCREMENT,
              name TEXT NOT NULL,
              instruction TEXT NOT NULL,
              hotkey TEXT NULL,
              use_count INTEGER NOT NULL DEFAULT 0
            );

            CREATE TABLE IF NOT EXISTS recent_instructions (
              id INTEGER PRIMARY KEY AUTOINCREMENT,
              text TEXT NOT NULL,
              used_utc TEXT NOT NULL
            );
            CREATE INDEX IF NOT EXISTS ix_recent_instructions_used ON recent_instructions(used_utc);

            CREATE TABLE IF NOT EXISTS action_profile_memory (
              action_id TEXT PRIMARY KEY,
              profile_name TEXT NOT NULL,
              updated_utc TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS endpoint_acknowledgments (
              profile_name TEXT NOT NULL,
              host TEXT NOT NULL,
              acknowledged_utc TEXT NOT NULL,
              PRIMARY KEY (profile_name, host)
            );

            CREATE TABLE IF NOT EXISTS caret_method_memory (
              process_name TEXT PRIMARY KEY COLLATE NOCASE,
              method TEXT NOT NULL,
              updated_utc TEXT NOT NULL
            );
            """)
    ];
}
