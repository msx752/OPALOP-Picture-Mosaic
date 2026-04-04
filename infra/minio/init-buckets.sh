#!/bin/sh
set -e

MINIO_HOST="${MINIO_HOST:-http://minio:9000}"
MINIO_ACCESS_KEY="${MINIO_ACCESS_KEY:-minioadmin}"
MINIO_SECRET_KEY="${MINIO_SECRET_KEY:-minioadmin}"
ALIAS="${MINIO_ALIAS:-local}"

echo "Waiting for MinIO to be ready..."
until mc alias set "$ALIAS" "$MINIO_HOST" "$MINIO_ACCESS_KEY" "$MINIO_SECRET_KEY"; do
  echo "MinIO not ready yet, retrying in 2 seconds..."
  sleep 2
done

echo "Creating buckets..."
mc mb --ignore-existing "$ALIAS/photos"
echo "  - photos bucket created"

mc mb --ignore-existing "$ALIAS/resources"
echo "  - resources bucket created"

mc mb --ignore-existing "$ALIAS/mosaics"
echo "  - mosaics bucket created"

echo "All buckets initialized successfully."
