namespace Yxy.Core.Data
{
    internal static class SchemaSql
    {
        public const string CreateAll = @"
CREATE TABLE IF NOT EXISTS settings (
    id INTEGER PRIMARY KEY CHECK (id = 1),
    bak_path TEXT NOT NULL,
    software_id TEXT NOT NULL,
    local_retain_days INTEGER NOT NULL DEFAULT 7,
    network_retain_days INTEGER NOT NULL DEFAULT 7,
    ftp_retain_days INTEGER NOT NULL DEFAULT 7,
    qiniu_retain_days INTEGER NOT NULL DEFAULT 7,
    purge_local INTEGER NOT NULL DEFAULT 0,
    purge_network INTEGER NOT NULL DEFAULT 0,
    purge_ftp INTEGER NOT NULL DEFAULT 0,
    purge_qiniu INTEGER NOT NULL DEFAULT 0,
    purge_time TEXT NOT NULL DEFAULT '00:30',
    updated_utc TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS license (
    id INTEGER PRIMARY KEY CHECK (id = 1),
    first_run_utc TEXT NOT NULL,
    account TEXT,
    token_protected TEXT,
    last_validated_utc TEXT
);

CREATE TABLE IF NOT EXISTS device_id (
    id INTEGER PRIMARY KEY CHECK (id = 1),
    device_id TEXT NOT NULL,
    created_utc TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS file_tasks (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    name TEXT NOT NULL,
    enabled INTEGER NOT NULL DEFAULT 1,
    source_kind TEXT NOT NULL DEFAULT 'local',
    source_path TEXT NOT NULL,
    retry_minutes INTEGER NOT NULL DEFAULT 5,
    sort_order INTEGER NOT NULL DEFAULT 0,
    cycle_kind TEXT NOT NULL DEFAULT 'day',
    cycle_interval INTEGER NOT NULL DEFAULT 1,
    next_run_utc TEXT,
    last_run_utc TEXT,
    run_count INTEGER NOT NULL DEFAULT 0,
    do_network INTEGER NOT NULL DEFAULT 0,
    do_ftp INTEGER NOT NULL DEFAULT 0,
    do_cloud INTEGER NOT NULL DEFAULT 0,
    cloud_provider TEXT,
    do_delete_now INTEGER NOT NULL DEFAULT 0,
    do_email INTEGER NOT NULL DEFAULT 0,
    post_program TEXT,
    created_utc TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS mssql_instances (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    name TEXT NOT NULL,
    server TEXT NOT NULL DEFAULT 'localhost',
    port INTEGER NOT NULL DEFAULT 0,
    auth_kind TEXT NOT NULL DEFAULT 'windows',
    username TEXT,
    password_protected TEXT,
    timeout_seconds INTEGER NOT NULL DEFAULT 30,
    enabled INTEGER NOT NULL DEFAULT 1,
    created_utc TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS mssql_tasks (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    name TEXT NOT NULL,
    instance_id INTEGER NOT NULL,
    enabled INTEGER NOT NULL DEFAULT 1,
    db_scope TEXT NOT NULL DEFAULT 'user',
    selected_dbs TEXT,
    retry_minutes INTEGER NOT NULL DEFAULT 5,
    sort_order INTEGER NOT NULL DEFAULT 0,
    cycle_kind TEXT NOT NULL DEFAULT 'day',
    cycle_interval INTEGER NOT NULL DEFAULT 1,
    next_run_utc TEXT,
    last_run_utc TEXT,
    run_count INTEGER NOT NULL DEFAULT 0,
    do_network INTEGER NOT NULL DEFAULT 0,
    do_ftp INTEGER NOT NULL DEFAULT 0,
    do_cloud INTEGER NOT NULL DEFAULT 0,
    cloud_provider TEXT,
    do_delete_now INTEGER NOT NULL DEFAULT 0,
    do_email INTEGER NOT NULL DEFAULT 0,
    created_utc TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS mysql_instances (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    name TEXT NOT NULL,
    server TEXT NOT NULL DEFAULT 'localhost',
    port INTEGER NOT NULL DEFAULT 3306,
    username TEXT,
    password_protected TEXT,
    timeout_seconds INTEGER NOT NULL DEFAULT 30,
    enabled INTEGER NOT NULL DEFAULT 1,
    created_utc TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS mysql_tasks (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    name TEXT NOT NULL,
    instance_id INTEGER NOT NULL,
    enabled INTEGER NOT NULL DEFAULT 1,
    db_scope TEXT NOT NULL DEFAULT 'user',
    selected_dbs TEXT,
    retry_minutes INTEGER NOT NULL DEFAULT 5,
    sort_order INTEGER NOT NULL DEFAULT 0,
    cycle_kind TEXT NOT NULL DEFAULT 'day',
    cycle_interval INTEGER NOT NULL DEFAULT 1,
    next_run_utc TEXT,
    last_run_utc TEXT,
    run_count INTEGER NOT NULL DEFAULT 0,
    do_network INTEGER NOT NULL DEFAULT 0,
    do_ftp INTEGER NOT NULL DEFAULT 0,
    do_cloud INTEGER NOT NULL DEFAULT 0,
    cloud_provider TEXT,
    do_delete_now INTEGER NOT NULL DEFAULT 0,
    do_email INTEGER NOT NULL DEFAULT 0,
    created_utc TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS media (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    serial_number TEXT NOT NULL,
    disk_signature TEXT NOT NULL,
    capacity_gb INTEGER NOT NULL,
    volume_guid TEXT,
    registered_utc TEXT NOT NULL,
    last_seen_utc TEXT
);

CREATE TABLE IF NOT EXISTS media_tasks (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    name TEXT NOT NULL,
    media_id INTEGER NOT NULL,
    enabled INTEGER NOT NULL DEFAULT 1,
    source_path TEXT,
    target_folder TEXT NOT NULL DEFAULT '用心云备份',
    window_start TEXT NOT NULL,
    window_end TEXT NOT NULL,
    wait_minutes INTEGER NOT NULL DEFAULT 30,
    retain_versions INTEGER NOT NULL DEFAULT 7,
    retain_days INTEGER NOT NULL DEFAULT 7,
    conflict_mode TEXT NOT NULL DEFAULT 'overwrite',
    isolate_after INTEGER NOT NULL DEFAULT 1,
    isolate_level INTEGER NOT NULL DEFAULT 2,
    use_sha256 INTEGER NOT NULL DEFAULT 0,
    cycle_kind TEXT NOT NULL DEFAULT 'day',
    cycle_interval INTEGER NOT NULL DEFAULT 1,
    next_run_utc TEXT,
    last_run_utc TEXT,
    run_count INTEGER NOT NULL DEFAULT 0,
    created_utc TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS cloud_accounts (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    provider TEXT NOT NULL,
    enabled INTEGER NOT NULL DEFAULT 0,
    fields_protected TEXT,
    created_utc TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS xfer_accounts (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    kind TEXT NOT NULL,
    fields_protected TEXT,
    created_utc TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS logs (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    type TEXT NOT NULL,
    mode TEXT NOT NULL DEFAULT 'auto',
    result TEXT NOT NULL,
    message TEXT,
    task_name TEXT,
    created_utc TEXT NOT NULL
);

CREATE INDEX IF NOT EXISTS ix_logs_created ON logs(created_utc);
CREATE INDEX IF NOT EXISTS ix_file_tasks_next ON file_tasks(next_run_utc);
CREATE INDEX IF NOT EXISTS ix_mssql_tasks_next ON mssql_tasks(next_run_utc);
CREATE INDEX IF NOT EXISTS ix_mysql_tasks_next ON mysql_tasks(next_run_utc);
CREATE INDEX IF NOT EXISTS ix_media_tasks_next ON media_tasks(next_run_utc);
";
    }
}
