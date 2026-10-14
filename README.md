# SvcTool

本仓库主要是用于将 exe 注册为 Windows 系统的服务，以便服务器/电脑重启之后程序依旧自动运行。

---

## 发布与下载架构

**GitHub 负责构建和发布，CNB 负责国内下载。**

GitHub Actions 跑在国外的机器上，构建没问题；但 Release 附件在国内下载很慢，所以每个版本都会自动镜像一份到 [CNB](https://cnb.cool/zmzhao/SvcTool)。

```
本地 push tag v1.2.3
        │
        ├─► Build and Release        windows-latest 构建 → 创建 GitHub Release
        │
        └─► Sync code to CNB         全部分支/标签镜像到 CNB
                    │
                    ▼
        Build and Release 建好 Release
                    │
                    ▼
        Mirror release to CNB        下载 GitHub 附件 → 上传到 CNB Release
```

三个 workflow 相互独立，任何一个失败都不影响另外两个，缺什么用 `workflow_dispatch` 补跑即可。

## 下载地址约定

程序内自动更新请使用下面这两个地址，**它们不会随版本变化**：

| 用途 | 地址 |
| --- | --- |
| 版本信息 | `https://cnb.cool/zmzhao/SvcTool/-/releases/latest/download/version.json` |
| 安装包 | `https://cnb.cool/zmzhao/SvcTool/-/releases/download/<tag>/<文件名>` |
| 安装包（最新版） | `https://cnb.cool/zmzhao/SvcTool/-/releases/latest/download/<文件名>` |

这些地址**不需要任何鉴权**（公开仓库），国内走 CDN。

`version.json` 结构（由 workflow 自动生成，字段保持稳定）：

```json
{
  "version": "1.2.3",
  "tag": "v1.2.3",
  "releaseDate": "2026-10-14T08:00:00Z",
  "files": [
    {
      "name": "SvcTool-1.2.3-win-x64.exe",
      "size": 64838889,
      "sha256": "……",
      "kind": "portable"
    }
  ],
  "mirrors": {
    "github": "https://github.com/Zhang6111/SvcTool/releases/tag/v1.2.3",
    "cnb": "https://cnb.cool/zmzhao/SvcTool/-/releases/tag/v1.2.3"
  }
}
```

`kind` 取值：`portable`（免安装单文件 exe）、`archive`（zip 压缩包）、`installer`（msi / setup.exe，暂未启用）、`other`。

## 发版步骤

版本号的唯一来源是 **tag**，`Directory.Build.props` 里的 `<Version>` 只是本地默认值：

```bash
git tag v1.2.3
git push origin v1.2.3
```

推完就等着。CI 会：

1. 校验 tag 符合 `v<major>.<minor>.<patch>`，不符合直接失败
2. `dotnet publish` 出自包含单文件 `SvcTool.exe`
3. 打包出 `.exe` 和 `.zip`，并生成 `version.json`
4. 创建 GitHub Release（附件：exe / zip / version.json）
5. 自动触发镜像，把上面三个文件发布到 CNB Release

**补跑**：任何一步失败，去 Actions 页面手动跑对应 workflow：

- 构建失败 → 跑 *Build and Release*，`tag` 填 `v1.2.3`
- 镜像失败 → 跑 *Mirror release to CNB*，`tag` 填 `v1.2.3`
- 代码没同步 → 跑 *Sync code to CNB*

## 一次性配置

### 1. GitHub Secrets

仓库 **Settings → Secrets and variables → Actions → New repository secret**：

| 名称 | 值 |
| --- | --- |
| `CNB_TOKEN` | CNB 访问令牌 |

令牌获取：CNB → 头像 → 个人设置 → 访问令牌 → 添加，常见场景选「仓库」，确保包含 `repo` 写权限。

### 2. 修改 CNB 仓库路径

当前硬编码为 `zmzhao/SvcTool`，散落在三个 workflow 的 `env.CNB_REPO`。换仓库时三个文件都要改。

### 3. 首次接入前先跑一遍实测

CNB 的附件上传接口没有在文档里写明期望的 HTTP 方法，所以首次接入前先用本地脚本确认一遍：

```powershell
.\scripts\cnb-smoke-test.ps1 -Token '<你的 CNB 令牌>'
```

脚本会依次验证令牌、tag、创建 Release、上传附件、匿名下载，最后打印结论。如果实测出的上传方式和 `.github/actions/cnb-release/action.yml` 里的默认值（`PUT`）不一致，改那个 input 即可。

## 本地开发

```powershell
dotnet build
dotnet run

# 本地复现 CI 的发布产物
dotnet publish SvcTool.csproj -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
  -p:Version=1.2.3 -o dist/exe
```

## 目录结构

```
.github/
  actions/cnb-release/      上传 Release 到 CNB 的复合 action
    action.yml
    publish.sh              实际干活的脚本，可脱离 GitHub Actions 单独调试
  workflows/
    build-release.yml       推 tag → 构建 → GitHub Release
    sync-cnb.yml            push → 代码/标签镜像到 CNB
    mirror-cnb.yml          GitHub Release → CNB Release 附件
scripts/
  cnb-smoke-test.ps1        本地实测 CNB 上传/下载链路
Directory.Build.props       版本号与程序集元数据的单一来源
```

## 注意事项

- **CNB 是只读镜像，不要直接在 CNB 上提交代码**，`sync-cnb.yml` 会强推覆盖。
- `sync-cnb.yml` 只强推分支和标签，**不会删除** CNB 上已被删掉的远端分支（git-sync 插件不支持 `--prune`）。需要时手动去 CNB 删。
- CNB 的 OpenAPI（`api.cnb.cool`）**即使对公开仓库也要求登录**，所以程序内更新**不要**调 API，只用上面「下载地址约定」里的网页直链。
- 目前只产出 `.exe` 和 `.zip`。将来要加 msi，改动集中在 `build-release.yml` 的打包步骤。
