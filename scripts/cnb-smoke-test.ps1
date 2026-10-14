<#
.SYNOPSIS
    本地验证 CNB 的 Release 上传/下载链路。

.DESCRIPTION
    在真正配 GitHub Actions 之前，用它先把唯一有不确定性的环节跑通：

      1. 访问令牌是否有效
      2. 目标 tag 在 CNB 上是否已存在（Release 依附于 tag）
      3. 能否创建 Release（POST /{repo}/-/releases）
      4. asset-upload-url 到底返回什么，用哪种 HTTP 方法能上传成功
      5. 上传后的文件能否「不带任何鉴权」下载 —— 这是程序内自动更新的前提

    跑完默认会删掉测试 Release；加 -Keep 保留，方便去网页上看效果。

    兼容 Windows PowerShell 5.1。

.EXAMPLE
    .\scripts\cnb-smoke-test.ps1 -Token 'xxxxxx'

.EXAMPLE
    # 保留测试 Release，换个 tag，方便对照
    .\scripts\cnb-smoke-test.ps1 -Token 'xxxxxx' -Tag 'v0.0.0-smoketest2' -Keep
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true, HelpMessage = 'CNB 访问令牌')]
    [string]$Token,

    [string]$Repo    = 'zmzhao/SvcTool',
    [string]$Api     = 'https://api.cnb.cool',
    [string]$WebBase = 'https://cnb.cool',
    [string]$Tag     = 'v0.0.0-smoketest',

    [switch]$Keep
)

$ErrorActionPreference = 'Stop'

$base   = "$($Api.TrimEnd('/'))/$Repo"
$web    = "$($WebBase.TrimEnd('/'))/$Repo"
$script:results = @()

function Write-Step { param([string]$Text) Write-Host "`n==> $Text" -ForegroundColor Cyan }
function Write-Ok   { param([string]$Text) Write-Host "  [OK]   $Text" -ForegroundColor Green }
function Write-Bad  { param([string]$Text) Write-Host "  [FAIL] $Text" -ForegroundColor Red }
function Write-Warn2{ param([string]$Text) Write-Host "  [WARN] $Text" -ForegroundColor Yellow }
function Add-Result { param([string]$Name, [bool]$Ok, [string]$Detail)
    $script:results += [pscustomobject]@{ 检查 = $Name; 结果 = $(if ($Ok) { 'OK' } else { 'FAIL' }); 详情 = $Detail }
}

function Invoke-CnbApi {
    param(
        [string]$Method = 'GET',
        [Parameter(Mandatory = $true)][string]$Url,
        $Body,
        [switch]$Anonymous
    )

    $headers = @{ Accept = 'application/json' }
    if (-not $Anonymous) { $headers['Authorization'] = "Bearer $Token" }

    $params = @{
        Method         = $Method
        Uri            = $Url
        Headers        = $headers
        UseBasicParsing = $true
        TimeoutSec     = 120
    }
    if ($PSBoundParameters.ContainsKey('Body') -and $null -ne $Body) {
        $json = $Body | ConvertTo-Json -Depth 10 -Compress
        # 必须传字节数组：直接传 string 时 Windows PowerShell 5.1 会按 ANSI 编码，
        # 中文会全变成 ?（实测踩过）。
        $params['Body']        = [System.Text.Encoding]::UTF8.GetBytes($json)
        $params['ContentType'] = 'application/json; charset=utf-8'
    }

    try {
        $r = Invoke-WebRequest @params
        return [pscustomobject]@{ Status = [int]$r.StatusCode; Body = [string]$r.Content }
    }
    catch {
        $resp = $_.Exception.Response
        if ($null -ne $resp) {
            $code  = [int]$resp.StatusCode
            $body  = ''
            try {
                $sr   = New-Object System.IO.StreamReader($resp.GetResponseStream())
                $body = $sr.ReadToEnd()
            } catch { }
            return [pscustomobject]@{ Status = $code; Body = $body }
        }
        return [pscustomobject]@{ Status = 0; Body = $_.Exception.Message }
    }
}

function Invoke-CurlRaw {
    param([Parameter(Mandatory = $true)][string[]]$Arguments)

    $argv = @('-sS') + $Arguments
    $out  = & curl.exe @argv 2>&1 | Out-String
    return $out.Trim()
}

Write-Host "CNB Release 链路实测" -ForegroundColor White
Write-Host "  API : $base"
Write-Host "  Web : $web"
Write-Host "  Tag : $Tag"

# ---------------------------------------------------------------- 1. token
Write-Step '1/6 校验访问令牌'
$me = Invoke-CnbApi -Url "$($Api.TrimEnd('/'))/user"
if ($me.Status -eq 200) {
    $user = $null
    try { $user = $me.Body | ConvertFrom-Json } catch { }
    $who = if ($user) { "$($user.username) ($($user.nickname))" } else { $me.Body }
    Write-Ok "令牌有效，当前用户：$who"
    Add-Result '令牌有效' $true $who
}
else {
    Write-Bad "HTTP $($me.Status) $($me.Body)"
    Add-Result '令牌有效' $false "HTTP $($me.Status)"
    Write-Host "`n令牌不可用，后面的检查没法进行。请到 CNB -> 头像 -> 个人设置 -> 访问令牌 重新生成。" -ForegroundColor Red
    exit 1
}

# ---------------------------------------------------------------- 2. tag
Write-Step '2/6 检查 tag 是否已在 CNB 上存在'
$tagsResp = Invoke-CnbApi -Url "$base/-/git/tags?page=1&page_size=100"
$tagExists = $false
if ($tagsResp.Status -eq 200) {
    $names = @()
    try {
        $parsed = $tagsResp.Body | ConvertFrom-Json
        if ($parsed -is [array]) { $names = $parsed | ForEach-Object { $_.name } }
        elseif ($parsed.PSObject.Properties.Name -contains 'tags') { $names = $parsed.tags | ForEach-Object { $_.name } }
        elseif ($parsed.PSObject.Properties.Name -contains 'list') { $names = $parsed.list | ForEach-Object { $_.name } }
    } catch { }
    $tagExists = ($names -contains $Tag)
    if ($tagExists) {
        Write-Ok "tag $Tag 已存在（CNB 上共 $($names.Count) 个 tag）"
    } else {
        Write-Warn2 "CNB 上没有 tag $Tag（共 $($names.Count) 个 tag）"
        Write-Host '        可以先在本地推送一个测试 tag：' -ForegroundColor Yellow
        Write-Host "          git tag $Tag && git remote add cnb $web.git && git push cnb $Tag" -ForegroundColor Yellow
        Write-Host '        下面仍会继续尝试，用来观察 API 在没有 tag 时的行为。' -ForegroundColor Yellow
    }
    Add-Result 'tag 存在' $tagExists "$($names.Count) 个 tag"
}
else {
    Write-Warn2 "查询 tag 失败：HTTP $($tagsResp.Status) $($tagsResp.Body)"
    Add-Result 'tag 存在' $false "查询接口返回 HTTP $($tagsResp.Status)"
}

# ---------------------------------------------------------------- 3. release
Write-Step '3/6 创建测试 Release'
$existing = Invoke-CnbApi -Url "$base/-/releases/tags/$Tag"
$releaseId = $null
if ($existing.Status -eq 200) {
    try { $releaseId = ($existing.Body | ConvertFrom-Json).id } catch { }
    Write-Ok "Release 已存在，id=$releaseId（将复用）"
    Add-Result '创建 Release' $true "已存在，id=$releaseId"
}
else {
    $created = Invoke-CnbApi -Method POST -Url "$base/-/releases" -Body @{
        tag_name       = $Tag
        name           = "$Tag (smoke test)"
        body           = '由 scripts/cnb-smoke-test.ps1 创建的测试 Release，可以安全删除。'
        draft          = $false
        prerelease     = $false
        make_latest    = 'false'
    }
    if ($created.Status -in 200, 201) {
        Write-Host "  响应：$($created.Body)"
        try { $releaseId = ($created.Body | ConvertFrom-Json).id } catch { }
        Write-Ok "创建成功，id=$releaseId"
        Add-Result '创建 Release' $true "id=$releaseId"
    }
    else {
        Write-Bad "HTTP $($created.Status) $($created.Body)"
        Add-Result '创建 Release' $false "HTTP $($created.Status)"
    }
}

if (-not $releaseId) {
    Write-Host "`n拿不到 release id，后面没法继续。" -ForegroundColor Red
    $script:results | Format-Table -AutoSize | Out-String | Write-Host
    exit 1
}

# ---------------------------------------------------------------- 4. upload-url
Write-Step '4/6 申请附件上传地址'
$testFile = Join-Path $env:TEMP ("cnb-smoke-$([guid]::NewGuid().ToString('N')).txt")
[System.IO.File]::WriteAllText($testFile, "cnb smoke test $(Get-Date -Format o)", [System.Text.Encoding]::UTF8)
$testName = Split-Path $testFile -Leaf
$testSize = (Get-Item $testFile).Length
Write-Host "  测试文件：$testName（$testSize 字节）"

$upResp = Invoke-CnbApi -Method POST -Url "$base/-/releases/$releaseId/asset-upload-url" -Body @{
    asset_name = $testName
    size       = $testSize
    overwrite  = $true
}

if ($upResp.Status -notin 200, 201) {
    Write-Bad "HTTP $($upResp.Status) $($upResp.Body)"
    Add-Result '申请上传地址' $false "HTTP $($upResp.Status)"
    Write-Host "`n这一步就失败了，说明接口路径或请求体字段不对，需要回头看 swagger。" -ForegroundColor Red
    $script:results | Format-Table -AutoSize | Out-String | Write-Host
    exit 1
}

Write-Host "  原始响应（关键，用来确定上传方式）："
Write-Host "  $($upResp.Body)"
$up = $null
try { $up = $upResp.Body | ConvertFrom-Json } catch { }
$uploadUrl = $up.upload_url
$verifyUrl = $up.verify_url
Write-Ok "upload_url = $uploadUrl"
if ($verifyUrl) { Write-Ok "verify_url = $verifyUrl" }
Add-Result '申请上传地址' $true "upload_url 已返回"

# ---------------------------------------------------------------- 5. upload
Write-Step '5/6 尝试各种上传方式，找出能用的那个'
$attempts = @(
    @{ Name = 'PUT  + --data-binary'; Args = @('-X', 'PUT', '--data-binary', "@$testFile", '-o', 'NUL', '-w', '%{http_code}', $uploadUrl) }
    @{ Name = 'PUT  + --upload-file'; Args = @('-X', 'PUT', '--upload-file', $testFile, '-o', 'NUL', '-w', '%{http_code}', $uploadUrl) }
    @{ Name = 'POST + --data-binary'; Args = @('-X', 'POST', '--data-binary', "@$testFile", '-o', 'NUL', '-w', '%{http_code}', $uploadUrl) }
    @{ Name = 'POST + multipart';     Args = @('-X', 'POST', '-F', "file=@$testFile", '-o', 'NUL', '-w', '%{http_code}', $uploadUrl) }
)

$working = $null
foreach ($a in $attempts) {
    $code = Invoke-CurlRaw -Arguments $a.Args
    $ok   = ($code -match '^2\d\d$')
    if ($ok) {
        Write-Ok "$($a.Name) -> HTTP $code"
        if (-not $working) { $working = $a.Name }
    }
    else {
        Write-Warn2 "$($a.Name) -> HTTP $code"
    }
}

if (-not $working) {
    Write-Bad '所有上传方式都失败了，需要看 swagger 或改用备选接口 POST /{repo}/-/upload/releases/{tagName}'
    Add-Result '上传附件' $false '全部方式失败'
}
else {
    Add-Result '上传附件' $true $working
}

# 确认上传
if ($working -and $verifyUrl) {
    $confirm = Invoke-CnbApi -Method POST -Url $verifyUrl
    if ($confirm.Status -in 200, 201, 204) {
        Write-Ok "确认上传成功 HTTP $($confirm.Status)"
    }
    else {
        Write-Warn2 "确认上传返回 HTTP $($confirm.Status)：$($confirm.Body)"
    }
}

# ---------------------------------------------------------------- 6. download
Write-Step '6/6 不带鉴权下载校验（程序内自动更新的前提）'
Start-Sleep -Seconds 2
$downloadUrls = @(
    "$web/-/releases/download/$Tag/$testName"
)
foreach ($url in $downloadUrls) {
    $got = Invoke-CurlRaw -Arguments @('-L', '-o', 'NUL', '-w', '%{size_download}', '--max-time', '60', $url)
    if ($got -eq "$testSize") {
        Write-Ok "匿名下载 OK（$got 字节）：$url"
        Add-Result '匿名下载' $true "$got 字节"
    }
    else {
        Write-Bad "匿名下载异常（期望 $testSize 字节，实际 '$got'）：$url"
        Add-Result '匿名下载' $false "实际 '$got' 字节"
    }
}

# ---------------------------------------------------------------- 清理
if (-not $Keep) {
    Write-Step '清理测试数据'
    $del = Invoke-CnbApi -Method DELETE -Url "$base/-/releases/$releaseId"
    if ($del.Status -in 200, 204) { Write-Ok "已删除测试 Release id=$releaseId" }
    else { Write-Warn2 "删除返回 HTTP $($del.Status)，请手动去 CNB 网页上删掉 $Tag" }
}
else {
    Write-Host "`n已保留测试 Release（-Keep），记得回头手动清理：" -ForegroundColor Yellow
    Write-Host "  $web/-/releases/tag/$Tag" -ForegroundColor Yellow
}

Remove-Item $testFile -Force -ErrorAction SilentlyContinue

# ---------------------------------------------------------------- 汇总
Write-Host "`n================ 汇总 ================" -ForegroundColor White
$script:results | Format-Table -AutoSize | Out-String | Write-Host

Write-Host '结论：' -ForegroundColor White
Write-Host "  - 上传方式请把 .github/actions/cnb-release/action.yml 的 upload-method 设为：" -ForegroundColor White
if ($working) {
    $method = if ($working -like 'PUT*') { 'PUT' } elseif ($working -like 'POST + multipart*') { 'PUT (见备注)' } else { 'POST' }
    Write-Host "      $method   （实测成功的是：$($working)）" -ForegroundColor Green
    if ($working -like 'POST + multipart*') {
        Write-Host '      注意：multipart 方式需要按返回的 form 字段调整 publish.sh，不能只改 method。' -ForegroundColor Yellow
    }
}
else {
    Write-Host '      暂无法确定，见上面的失败信息' -ForegroundColor Red
}
