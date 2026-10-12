# Security Policy / セキュリティポリシー

本ドキュメントは「かぶ自動LOGIN」のセキュリティ設計と脆弱性報告の窓口について説明します。

## 暗号化設計

### パスワード保存
- **Windows Data Protection API (DPAPI)** を使用
- `DataProtectionScope.CurrentUser` でユーザーアカウントに紐付け
- 追加 Entropy として固定文字列 `KabuMSLauncher.v1.Bostok` を併用
- 結果として暗号化された Base64 文字列を JSON ファイルに保存

### 保存場所
```
%LOCALAPPDATA%\KabuMSLauncher\settings.json
```

### 復号条件
- 同一 PC かつ同一 Windows ユーザーアカウントでのみ復号可能
- 設定ファイルだけを他 PC やユーザーにコピーしても復号不可
- exe バイナリだけを盗まれても、設定ファイルがなければ意味なし

### 比較：他のツールとの差
| ツール | パスワード保存方式 | 同等性 |
|---|---|---|
| Google Chrome | DPAPI + AES | ほぼ同等 |
| Microsoft Edge | DPAPI + AES | ほぼ同等 |
| かぶ自動LOGIN | DPAPI + 固定 Entropy | 軽量版 |

## 通信

**ID/パスワードを外部に送信することはありません。**

1.0.x は外部と通信しません。1.1.0 から、外部と通信するのは次の2つの場合だけです。

1. **通信が切れたときの接続確認**：MarketspeedⅡ の「サーバとの通信が切断されました」を見つけたとき、インターネットが戻ったかを確かめるため、8.8.8.8・1.1.1.1 へ ping し、www.rakuten-sec.co.jp の443番へ接続を試みます。データは送りません。
2. **メール通知（任意）**：利用者が設定した場合だけ、登録したメールアドレスと再接続の経過（時刻と操作の記録）を、当社の受け口（Cloudflare Workers）へ HTTPS で送ります。受け口は、確認コードで確かめたアドレスにだけ、決まった文面で送ります。

- テレメトリ・アナリティクス等の収集機能はありません
- ログイン機能だけを使う場合は、ネット接続がなくても動作します

## 範囲

本ツールが自動化する範囲：
- ✅ MarketspeedⅡ.exe の起動
- ✅ ログイン画面のID入力
- ✅ ログイン画面のパスワード入力
- ✅ ログインボタンの押下
- ✅ 通信切断の画面（「サーバとの通信が切断されました」）の OK（1.1.0 から）
- ✅ Excel の楽天RSS「未接続」ボタン（1.1.0 から。「発注不可」ボタンは利用者が設定で選んだ場合だけ）

本ツールが**意図的に自動化しない**範囲：
- ❌ **追加認証（メールでの絵柄合わせ）** — セキュリティ強度を保つため
- ❌ API アクセス
- ❌ 注文の発注
- ❌ ポジション管理・データ取得

## 脆弱性報告

セキュリティ上の問題を発見された場合、公開 Issue ではなく以下までご連絡ください：

📧 **info@cabu.info**

件名に `[Security]` を付けていただけると優先的に対応します。

## 楽天証券との関係

本ツールは MarketspeedⅡ のログインと再接続の操作を補助するツールであり、楽天証券株式会社とは無関係の独立した第三者開発ツールです。
本ツールが楽天証券のサーバーに接続するのは、通信が切れたときにインターネットの復旧を確かめる接続確認だけです（1.1.0 から）。取引の通信はすべて MarketspeedⅡ 本体が行います。

## ソースコード

本リポジトリで全ソースを公開しています。セキュリティ主張の検証用に、ご自身でビルドして比較することも可能です。
特に確認していただきたいファイル：

- [`KabuMSLauncher/Services/CredentialStore.cs`](KabuMSLauncher/Services/CredentialStore.cs) — DPAPI 暗号化の実装
- [`KabuMSLauncher/Services/AppSettings.cs`](KabuMSLauncher/Services/AppSettings.cs) — 設定ファイル読み書き
- [`KabuMSLauncher/Services/MarketSpeedLauncher.cs`](KabuMSLauncher/Services/MarketSpeedLauncher.cs) — 起動・自動入力ロジック
