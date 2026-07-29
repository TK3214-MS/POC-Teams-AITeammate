#!/usr/bin/env bash
set -euo pipefail
set +x

if [[ $# -ne 2 ]]; then
  echo "Usage: $0 <resource-group> <search-service-name>" >&2
  exit 2
fi

for command_name in az curl jq; do
  if ! command -v "$command_name" >/dev/null 2>&1; then
    echo "Required command not found: $command_name" >&2
    exit 2
  fi
done

resource_group="$1"
search_service_name="$2"
index_name="knowledge-index"
api_version="2024-07-01"
script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
schema_file="${script_dir}/../infra/search/knowledge-index.json"
response_file="$(mktemp "${TMPDIR:-/tmp}/knowledge-index.XXXXXX")"
trap 'rm -f "$response_file"' EXIT

admin_key="$(az search admin-key show \
  --resource-group "$resource_group" \
  --service-name "$search_service_name" \
  --query primaryKey \
  --output tsv \
  --only-show-errors)"

if [[ -z "$admin_key" ]]; then
  echo "Azure AI Search admin key lookup returned an empty value." >&2
  exit 1
fi

endpoint="https://${search_service_name}.search.windows.net/indexes/${index_name}?api-version=${api_version}"
http_status="$(
  printf 'header = "api-key: %s"\n' "$admin_key" |
    curl --config - --silent --show-error \
      --output "$response_file" \
      --write-out '%{http_code}' \
      "$endpoint"
)"
unset admin_key

if [[ "$http_status" == "200" ]]; then
  required_fields=(
    Id TenantId MeetingId SessionId Title Content Summary Category Status
    SourceSpeaker MeetingSubject MeetingDate Language Tags Confidence CreatedAt
    UpdatedAt ContentVector
  )

  for field_name in "${required_fields[@]}"; do
    if ! jq -e --arg field_name "$field_name" \
      '.fields | any(.name == $field_name)' "$response_file" >/dev/null; then
      echo "Existing index is missing required field: $field_name" >&2
      exit 1
    fi
  done

  if ! jq -e '
    (.fields[] | select(.name == "ContentVector") |
      .dimensions == 3072 and .vectorSearchProfile == "knowledge-vector-profile") and
    (.semantic.configurations | any(.name == "knowledge-semantic-config"))
  ' "$response_file" >/dev/null; then
    echo "Existing index has an incompatible vector or semantic configuration." >&2
    exit 1
  fi

  echo "Validated existing Azure AI Search index: $index_name"
  exit 0
fi

if [[ "$http_status" != "404" ]]; then
  echo "Failed to inspect Azure AI Search index (HTTP $http_status)." >&2
  jq -r '.error.message // empty' "$response_file" >&2
  exit 1
fi

admin_key="$(az search admin-key show \
  --resource-group "$resource_group" \
  --service-name "$search_service_name" \
  --query primaryKey \
  --output tsv \
  --only-show-errors)"

http_status="$(
  printf 'header = "api-key: %s"\n' "$admin_key" |
    curl --config - --silent --show-error \
      --request PUT \
      --header 'Content-Type: application/json' \
      --data-binary "@${schema_file}" \
      --output "$response_file" \
      --write-out '%{http_code}' \
      "$endpoint"
)"
unset admin_key

if [[ "$http_status" != "200" && "$http_status" != "201" ]]; then
  echo "Failed to create Azure AI Search index (HTTP $http_status)." >&2
  jq -r '.error.message // empty' "$response_file" >&2
  exit 1
fi

echo "Created Azure AI Search index: $index_name"