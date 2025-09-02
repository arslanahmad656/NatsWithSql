#!/bin/sh
set -e

echo "Waiting for SQL Server to be ready..."

until /opt/mssql-tools/bin/sqlcmd -S sqlserver -U sa -P "webdir123R" -Q "SELECT 1" > /dev/null 2>&1
do
  echo "SQL Server not ready yet. Retrying..."
  sleep 2
done

echo "SQL Server is ready!"
exec "$@"
