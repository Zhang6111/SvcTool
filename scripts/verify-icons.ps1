# 逐个解析 Icons.xaml 里的几何图形定义，确认它们在 WPF 里能被真正解析出来。
#
# 为什么需要这个检查：几何图形的 Figures 字符串是在运行时由类型转换器解析的，
# 编译期不校验。写错一个字符构建照样通过，但程序一启动就会崩。
# 这个脚本用 WPF 自己的解析器逐个过一遍，把问题提前暴露出来。
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName PresentationFramework
Add-Type -AssemblyName PresentationCore

$xamlPath = Join-Path $PSScriptRoot '..\src\SvcTool\Themes\Icons.xaml'
[xml]$doc = Get-Content $xamlPath -Raw -Encoding UTF8

$ns = 'http://schemas.microsoft.com/winfx/2006/xaml/presentation'
$xns = 'http://schemas.microsoft.com/winfx/2006/xaml'

$total = 0
$failed = 0

foreach ($node in $doc.ResourceDictionary.ChildNodes) {
    if ($node.NodeType -ne 'Element') { continue }

    $key = $node.GetAttribute('Key', $xns)
    if (-not $key) { continue }

    # 样式元素依赖其它资源（BasedOn 等），脱离完整字典无法独立解析，
    # 这里只校验几何图形。样式能否解析由 ThemeXamlTests 在应用上下文中验证。
    if ($node.LocalName -eq 'Style') { continue }

    # OuterXml 已经带了在作用域内的命名空间声明，不需要再手动补
    $inner = $node.OuterXml

    $total++
    try {
        $parsed = [System.Windows.Markup.XamlReader]::Parse($inner)
        if ($null -eq $parsed) {
            Write-Host ("[空]   {0}" -f $key) -ForegroundColor Red
            $failed++
            continue
        }

        # 只对几何图形检查包围盒。Style 之类的不是几何图形，解析成功即可。
        if ($parsed -isnot [System.Windows.Media.Geometry]) {
            Write-Host ("[OK]   {0,-24} （非几何图形，已成功解析）" -f $key)
            continue
        }

        $bounds = $parsed.Bounds
        if ($bounds.IsEmpty) {
            Write-Host ("[无效] {0}  包围盒为空" -f $key) -ForegroundColor Red
            $failed++
            continue
        }

        Write-Host ("[OK]   {0,-24} {1}" -f $key, $bounds.ToString())
    }
    catch {
        Write-Host ("[失败] {0,-24} {1}" -f $key, $_.Exception.Message) -ForegroundColor Red
        $failed++
    }
}

Write-Host ""
if ($failed -gt 0) {
    Write-Host ("检查失败：共 {0} 个，其中 {1} 个有问题。" -f $total, $failed) -ForegroundColor Red
    exit 1
}

Write-Host ("检查通过：{0} 个几何图形全部能被 WPF 解析。" -f $total) -ForegroundColor Green
exit 0
