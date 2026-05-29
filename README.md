# かぶ自動LOGIN — MarketspeedⅡ 自動起動アプリ

楽天証券 **MarketspeedⅡ** のログイン作業をワンクリックで自動化する、完全無料の Windows ランチャーです。
毎朝の起動・ID/パスワード入力・ログインボタン押下までを、ボタン1つで完了させます。

[![Latest Release](https://img.shields.io/github/v/release/Gohan-mada/kabu-ms-launcher?label=最新リリース)](https://github.com/Gohan-mada/kabu-ms-launcher/releases/latest)
[![License](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)
[![Platform](https://img.shields.io/badge/Windows-10%20%7C%2011-0078D6)](https://www.microsoft.com/windows)

---

## ⬇ ダウンロード

➡ **[公式LP（最新版＋使い方ガイド）](https://cabu-labo.co.jp/login_ms.html)**

または直接ダウンロード：

➡ **[最新版インストーラ](https://github.com/Gohan-mada/kabu-ms-launcher/releases/latest/download/KabuMSLauncher_Setup.exe)**

---

## ✨ 主な機能

- ⚡ **ワンクリック自動ログイン** — MarketspeedⅡ の起動から認証情報入力、ログインボタン押下までを自動実行
- 🔒 **Windows DPAPI 暗号化** — パスワードは Microsoft 標準の Data Protection API で暗号化保存（Chrome / Edge と同じ仕組み）
- 🚀 **Windows 起動時の自動起動オプション** — PC 電源 ON から即トレード画面へ
- 📦 **軽量・追加インストール不要** — 本体 約30KB、インストーラ 約2MB
- 💰 **完全無料・広告なし**

## 🔐 セキュリティ

- パスワードは **Windows DPAPI** で暗号化（[詳細](https://learn.microsoft.com/dotnet/standard/security/how-to-use-data-protection)）
- 暗号化キーは Windows アカウントに紐付き、**同一PC・同一ユーザーアカウントでのみ復号可能**
- **外部サーバーへの通信は一切ありません**（完全ローカル動作）
- API アクセス・自動売買等の機能は実装していません

詳細は [SECURITY.md](SECURITY.md) を参照。

## 📋 動作環境

| 項目 | 要件 |
|---|---|
| OS | Windows 10（バージョン 1903 以降）／ Windows 11 |
| ランタイム | .NET Framework 4.8（Windows 標準搭載・**追加インストール不要**） |
| 前提アプリ | MarketspeedⅡ（楽天証券・別途インストール必要） |

## 🎬 デモ動画

YouTube: [かぶ自動LOGIN 1分でわかる使い方](https://youtu.be/PLACEHOLDER)

## 🚀 使い方

詳細は [公式ガイド](https://cabu-labo.co.jp/login_ms.html) をご覧ください。

1. インストーラをダウンロード → 実行（管理者権限不要）
2. アプリを起動 → 楽天証券のID/パスワードを入力 → 「保存する」にチェック
3. 「⚡ MarketspeedⅡ を起動する」ボタンをクリック

> ⚠ **追加認証について**
> MarketspeedⅡ の追加認証（メールでの絵柄合わせ）は、セキュリティの観点から**あえて自動化していません**。
> 追加認証画面が表示された場合のみ、ご自身で絵柄をお選びください。

## 🛠 ビルド方法

開発者向け。Visual Studio Build Tools 2022 / 2026 と .NET Framework 4.8 Developer Pack（または同梱の NuGet 参照アセンブリ）が必要です。

```powershell
# ビルド
msbuild .\KabuMSLauncher\KabuMSLauncher.csproj /p:Configuration=Release

# 成果物: .\KabuMSLauncher\bin\Release\KabuMSLauncher.exe

# インストーラのビルド（Inno Setup 6 必要）
& "C:\Program Files (x86)\Inno Setup 6\ISCC.exe" .\installer\KabuMSLauncher.iss
```

## 🤝 提供元・運営

[**かぶ自動化LAB**](https://cabu-labo.co.jp/) — 株式会社ボストーク
個人投資家向け AI 活用学習メディア

- 🌐 公式サイト：[cabu.info](https://cabu-labo.co.jp/)
- 📧 お問い合わせ：info@cabu.info

## 📝 免責事項

本ツールは MarketspeedⅡ のログインを補助するローカルツールであり、**楽天証券株式会社とは無関係の独立した第三者開発ツール**です。
本ツールの利用により生じたいかなる損害についても開発元は責任を負いません。
本ツールは投資助言・銘柄推奨を一切行いません。投資判断はご自身の責任で行ってください。

## 📄 ライセンス

[MIT License](LICENSE)
