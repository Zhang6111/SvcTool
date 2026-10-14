#!/usr/bin/env bash
#
# 在 CNB 上创建/更新 Release，并上传一个目录下的全部文件作为附件。
# 由 .github/actions/cnb-release/action.yml 调用。
#
# 想单独调试的话，导出这些环境变量后直接跑即可：
#   CNB_API=https://api.cnb.cool CNB_REPO=zmzhao/SvcTool CNB_TAG=v0.1.0 \
#   CNB_DIR=dist CNB_TOKEN=xxx bash publish.sh
#
set -euo pipefail

: "${CNB_API:?缺少 CNB_API}"
: "${CNB_REPO:?缺少 CNB_REPO}"
: "${CNB_TAG:?缺少 CNB_TAG}"
: "${CNB_DIR:?缺少 CNB_DIR}"
: "${CNB_TOKEN:?缺少 CNB_TOKEN}"

api="${CNB_API%/}/$CNB_REPO"
upload_method="${CNB_UPLOAD_METHOD:-PUT}"
make_latest="${CNB_MAKE_LATEST:-true}"

AUTH=(-H "Authorization: Bearer $CNB_TOKEN" -H 'Accept: application/json')

say()  { printf '\n==> %s\n' "$*"; }
fail() { printf 'ERROR: %s\n' "$*" >&2; exit 1; }

# --------------------------------------------------------------- 1. Release
say "查询 release：$CNB_TAG"
resp="$(curl -sS -w $'\n__HTTP__%{http_code}' "${AUTH[@]}" "$api/-/releases/tags/$CNB_TAG")"
code="${resp##*__HTTP__}"
payload="${resp%$'\n'__HTTP__*}"

patch_body="$(jq -n \
  --arg n "${CNB_NAME:-$CNB_TAG}" \
  --arg b "${CNB_BODY:-}" \
  --arg ml "$make_latest" \
  '{name:$n, body:$b, draft:false, prerelease:false, make_latest:$ml}')"

if [ "$code" = "200" ]; then
  release_id="$(jq -r '.id' <<<"$payload")"
  echo "已存在，id=$release_id，更新标题/说明"
  curl -sS -X PATCH "${AUTH[@]}" -H 'Content-Type: application/json' \
       -d "$patch_body" "$api/-/releases/$release_id" >/dev/null
else
  echo "不存在（http $code），创建中……"
  create_body="$(jq -n \
    --arg t "$CNB_TAG" \
    --arg n "${CNB_NAME:-$CNB_TAG}" \
    --arg b "${CNB_BODY:-}" \
    --arg ml "$make_latest" \
    '{tag_name:$t, name:$n, body:$b, draft:false, prerelease:false, make_latest:$ml}')"
  echo "请求体：$create_body"

  payload="$(curl -sS -f -X POST "${AUTH[@]}" -H 'Content-Type: application/json' \
             -d "$create_body" "$api/-/releases")" \
    || fail "创建 release 失败，响应：$payload"

  release_id="$(jq -r '.id // empty' <<<"$payload")"
  [ -n "$release_id" ] || fail "创建 release 后没拿到 id，响应：$payload"
  echo "已创建，id=$release_id"
fi

# ---------------------------------------------------------------- 2. Assets
mapfile -t files < <(find "$CNB_DIR" -maxdepth 1 -type f | sort)
[ "${#files[@]}" -gt 0 ] || fail "$CNB_DIR 下没有可上传的文件"

for file in "${files[@]}"; do
  name="$(basename "$file")"
  size="$(stat -c '%s' "$file")"
  say "上传 $name（$size 字节）"

  up_body="$(jq -n --arg n "$name" --argjson s "$size" \
    '{asset_name:$n, size:$s, overwrite:true}')"

  up="$(curl -sS -f -X POST "${AUTH[@]}" -H 'Content-Type: application/json' \
        -d "$up_body" "$api/-/releases/$release_id/asset-upload-url")" \
    || fail "申请上传地址失败：$name"

  echo "asset-upload-url 响应：$up"
  upload_url="$(jq -r '.upload_url // empty' <<<"$up")"
  verify_url="$(jq -r '.verify_url // empty' <<<"$up")"
  [ -n "$upload_url" ] || fail "响应里没有 upload_url：$up"

  curl -sS -f -X "$upload_method" --data-binary "@$file" "$upload_url" \
    || fail "上传失败（方法 $upload_method）：$name"

  if [ -n "$verify_url" ]; then
    curl -sS -f -X POST "${AUTH[@]}" "$verify_url" || fail "确认上传失败：$name"
  fi

  echo "完成：$name"
done

say "全部完成，共 ${#files[@]} 个文件"
