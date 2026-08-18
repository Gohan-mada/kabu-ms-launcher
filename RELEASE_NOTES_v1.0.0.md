# v1.0.0 — 初回リリース 🎉

楽天証券の MarketspeedⅡ のログイン操作を1クリックで自動化する Windows ランチャーの初回リリースです。

## ⬇ ダウンロード

➡ **[KabuMSLauncher_Setup.exe](https://github.com/Gohan-mada/kabu-ms-launcher/releases/download/v1.0.0/KabuMSLauncher_Setup_1.0.0.exe)** （推奨）

## ✨ 主な機能

- 🚀 MarketspeedⅡ の起動からログイン完了まで完全自動化
- 🔒 Windows DPAPI による ID/パスワード暗号化保存
- ⚙ Windows 起動時の自動起動オプション
- 📦 軽量設計（本体約 500KB、インストーラ約 2.5MB）
- 🎨 Tomoru（灯） ブランド対応UI

## 動作環境

- Windows 10（バージョン 1903 以降）／ Windows 11
- .NET Framework 4.8（Windows 標準搭載・追加インストール不要）
- MarketspeedⅡ（楽天証券・別途インストール必要）

## インストール方法

ダウンロードしたセットアップを実行してください。管理者権限は不要です。

> ⚠ 初回起動時に **Windows SmartScreen** の警告が表示される場合があります。
> 「詳細情報」→「実行」で起動できます。これは未署名アプリ全般に出る Windows 標準の警告で、本アプリが危険なわけではありません。
> コードサイニング証明書取得後、表示されなくなる予定です。

## 重要：追加認証は自動化していません

MarketspeedⅡ の追加認証（メールでの絵柄合わせ）は、**セキュリティの観点からあえて自動化しておりません**。
追加認証画面が表示された場合のみ、ご自身で絵柄をお選びください。

## セキュリティ

- パスワードは Windows DPAPI（Data Protection API）で暗号化
- 同一 PC・同一ユーザーアカウントでのみ復号可能
- 外部通信は一切なし
- 詳細は [SECURITY.md](https://github.com/Gohan-mada/kabu-ms-launcher/blob/main/SECURITY.md)

## 免責

本ツールは MarketspeedⅡ のログインを補助するローカルツールであり、楽天証券株式会社とは無関係の独立した第三者開発ツールです。
本ツールの利用により生じたいかなる損害についても開発元は責任を負いません。
投資判断はご自身の責任で行ってください。

---
提供：株式会社ボストーク
公式：https://cabu-labo.co.jp/
