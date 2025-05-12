# Caching Proxy Server

## 概要

これは、[roadmap.sh の "Caching Proxy Server" プロジェクト](https://roadmap.sh/projects/caching-proxy) の要件に基づいて作成された、シンプルな HTTP キャッシュプロキシサーバーです。
指定されたポートで HTTP リクエストを受け付け、設定されたオリジンサーバーにリクエストを転送します。オリジンサーバーからの成功レスポンス (HTTP 2xx) はメモリ内にキャッシュされ、次回同じリクエストがあった際にはキャッシュから応答を返します。

## 目的

*   HTTP プロキシサーバーの基本的な仕組みを理解する。
*   HTTP リクエスト/レスポンスの処理方法を学ぶ。
*   インメモリキャッシュの実装方法を学ぶ。
*   C# と ASP.NET Core Minimal API、System.CommandLine を使用した CLI ツールの開発経験を得る。

## 機能

*   指定されたポートでの HTTP リクエスト待受。
*   指定されたオリジンサーバーへの HTTP リクエスト転送。
*   成功したレスポンス (HTTP 2xx) のインメモリキャッシュ。
    *   キャッシュキー: `HTTPメソッド:パス?クエリ文字列` (例: `GET:/products/1`)
    *   キャッシュ内容: ステータスコード、レスポンスヘッダー、レスポンスボディ (バイト配列)
*   キャッシュヒット/ミスを示す `X-Cache` ヘッダーの付与 (`X-Cache: HIT` または `X-Cache: MISS`)。
*   CLI からの起動オプション:
    *   `--port <ポート番号>`: プロキシサーバーがリッスンするポート (デフォルト: 8080)。
    *   `--origin <オリジンURL>`: リクエスト転送先のサーバーURL (必須)。
*   キャッシュクリア用コマンド (`--clear-cache`):
    *   現状では、インメモリキャッシュのため、このコマンド実行時にはサーバー再起動が必要である旨のメッセージを表示します。

## 技術スタック

*   **言語:** C# (.NET 8 またはそれ以降)
*   **フレームワーク:** ASP.NET Core (Minimal API)
*   **ライブラリ:**
    *   `System.CommandLine`: CLI 引数の解析とコマンドハンドリング
    *   `System.Collections.Concurrent.ConcurrentDictionary`: スレッドセーフなインメモリキャッシュの実装

## プロジェクト構成
Use code with caution.
Markdown
MyCachingServer/
├── src/
│ └── CachingProxyServer/
│ ├── Program.cs # メインロジック、Webサーバー設定、リクエスト処理
│ ├── CachedResponse.cs # キャッシュされるレスポンスデータを保持するクラス
│ └── CachingProxyServer.csproj # プロジェクトファイル、依存関係
├── .gitignore # Git で無視するファイル/フォルダの設定
└── README.md # このファイル
## セットアップと実行方法

### 前提条件

*   [.NET SDK](https://dotnet.microsoft.com/ja-jp/download) (バージョン 8.0 またはそれ以降) がインストールされていること。

### 手順

1.  **(任意)** このリポジトリをクローンします:
    ```bash
    git clone <リポジトリURL>
    cd MyCachingServer
    ```

2.  プロジェクトをビルドします (任意ですが、依存関係の復元も行われます):
    ```bash
    dotnet build ./src/CachingProxyServer
    ```

3.  プロキシサーバーを実行します:
    ```bash
    dotnet run --project ./src/CachingProxyServer -- --port <ポート番号> --origin <オリジンURL>
    ```
    *   `<ポート番号>` をプロキシサーバーがリッスンするポート番号に置き換えます (例: `3000`)。
    *   `<オリジンURL>` をリクエスト転送先のベースURLに置き換えます (例: `http://dummyjson.com` や `https://httpbin.org`)。
    *   `--` の後にプロキシサーバーへの引数を指定することに注意してください。

    **実行例:**
    ```bash
    dotnet run --project ./src/CachingProxyServer -- --port 3000 --origin http://dummyjson.com
    ```
    コンソールに `Starting caching proxy server on port 3000 for origin http://dummyjson.com...` と表示されれば起動成功です。

4.  キャッシュクリアコマンドを実行します (サーバーは停止していても、別ターミナルからでも可):
    ```bash
    dotnet run --project ./src/CachingProxyServer -- --clear-cache
    ```
    コンソールにキャッシュクリアには再起動が必要である旨のメッセージが表示されます。

## 使用例

プロキシサーバーをポート 3000、オリジン `http://dummyjson.com` で起動した場合:

```bash
# サーバー起動
dotnet run --project ./src/CachingProxyServer -- --port 3000 --origin http://dummyjson.com
Use code with caution.
別のターミナルから curl を使ってアクセスします (-i でヘッダーも表示):
# 1回目のリクエスト (キャッシュミス)
curl -i http://localhost:3000/products/5

# 応答ヘッダーに "X-Cache: MISS" が含まれるはず
# サーバーログに "Cache MISS", "Cached response..." が表示される

# 2回目のリクエスト (キャッシュヒット)
curl -i http://localhost:3000/products/5

# 応答ヘッダーに "X-Cache: HIT" が含まれるはず
# サーバーログに "Cache HIT" が表示される
# (応答が1回目より速い可能性がある)