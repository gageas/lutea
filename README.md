# H2k6風 検索志向データベース型オーディオプレーヤ Lutea
Latest Version: 1.11 (2014/08/14)

## スクリーンショット
![SS1](http://lutea.gageas.com/lutea/raw-attachment/wiki/moreScreenshots/v1.11-main.png)  
![SS2](http://lutea.gageas.com/lutea/raw-attachment/wiki/moreScreenshots/v1.00-taskbar.png) 

 * [wiki:moreScreenshots 他のスクリーンショットをもっと見る]

## 説明

Luteaは、 __「一万曲でもサクサク気持ちよく検索できること」__
をテーマに開発しているWindows PC向けオーディオプレーヤです。

### 動作環境
    * Windows xp 以降
    * .Net Framework 4

### 主な機能
  * H2k6形式の __ライブラリ機能__
  * __SQL__ 、 __正規表現__ および __migemo__ による検索
  * H2k6形式のクエリファイル(.qファイル)による __動的プレイリスト__
  * __CUESheet__ , Internal CUESheetにネイティブで対応
  * __多種のコーデック__ に対応  
    MP2, MP3, WMA, OggVorbis, AAC(mp4/m4a), AppleLossless, Monkey'sAudio(ape), FLAC, TTA, WavPack, TAK あたり
  * __多種のタグ__ に対応  
    ID3V1, ID3V2, WMA, Vorbis, MP4, APETAG, FLAC
    * Multiple valuesに対応
  * __ジャケット画像表示__ 対応  
    タグ埋め込み画像, folder.jpg
  * __ギャップレス再生__
  * __ReplayGain__ 対応
  * __WASAPI共有/排他__ 出力対応
  * __プラグイン機構__ 下記のプラグインが同梱されています
    * 標準GUI
    * WebベースUI
    * last.fm scrobbler
  * プレイリストビュー内での'''viライクなキーバインド'''
  * 元のファイルは一切変更しない

### 足りない機能
  * zipとかの読み込みできません。
  * EQ、ASIO対応していません。

### 使い方
  * 全てのファイルを展開してLutea.exeを実行してください。
  * メニューバーのライブラリ → ディレクトリの追加... からファイルを取り込みます。
  * クエリ入力欄  
    画面下の長いテキストボックスがクエリ入力欄です。  
    この内容からプレイリストが生成されます。  
    [wiki:QueryStringDetail クエリ文字列詳細]
  * ショートカットキー
    * プレイリストビュー
      * H,J,K,L = カーソルの移動
      * Shift+J = 次のアルバムの先頭トラックまで移動
      * Shift+K = 前のアルバムの先頭トラックまで移動
      * Ctrl+J = 選択中のトラックを再生
      * Ctrl+M = 選択中のトラックを再生
      * Ctrl+N = 次のトラックへ
      * Ctrl+P = 前のトラックへ
      * gg = プレイリストの先頭へ
      * G = プレイリストの末尾へ
      * / = クエリ入力欄に移動
      * 0～5 = 選択したトラックの評価を★n個に変更
    * クエリ入力欄
      * EnterまたはEsc = プレイリストビューに移動

### [wiki:FAQ FAQ(ありがちな質問)]

### 出力について
   * 出力デバイスは，コンポーネント → コンポーネントの詳細設定 → Core で設定することができます。
   * 現在の出力モードはタイトルバーの末尾に表示されます。各モードの意味は下記の通りです。
     * STOP ： 停止
     * WASAPI ： WASAPI共有モード
     * WASAPIEx ： WASAPI排他モード
     * FloatingPoint : 32Bit浮動小数点出力

### アンインストール
Luteaを展開したフォルダを削除してください。  
レジストリは使用していません。

### ライセンスについて
NYSLまたはMITを適用します。[wiki:license 詳しく]

### 使用ライブラリ等一覧
  * データベースエンジンに[SQLite](http://www.sqlite.org/)を使用しています。
  * サウンド再生エンジンに[BASS Audio](http://www.un4seen.com/)を使用しています。
  * ローマ字検索に[C/migemo](http://www.kaoriya.net/software/cmigemo)を使用しています。
  * 正規表現エンジンに[re2](http://code.google.com/p/re2/)を使用しています。
  * アイコンは[奥野アイコンメーカー](http://gyu.que.jp/oqunomaker/oqunomaker.html)で作成しました。

### [wiki:VersionHistory バージョン履歴]

## ダウンロード
[wiki:Downloads ダウンロードページ]

----
# コンタクト
twitter: [@gageas](http://twitter.com/gageas)
