/* 本プロジェクトをコンパイルするには，re2winのソースコードを取得し，ヘッダファイルをインクルードパスに入れてください */

#define POOL_NUM (16)
#define NOMINMAX 

#include <math.h>
#include <sys/types.h>
#include <sys/timeb.h>
#include <string>
#include <map>
#include "stdafx.h"
#include "windows.h"

#include "LuteaHelper.h"
#include "sqlite3.h"
#include "regexp.h"

typedef unsigned int uint;

using namespace std;


namespace Gageas{
	namespace Lutea{
		namespace Core {
			static map<string, gageas::regexp*> regex_cache; // コンパイル済み正規表現のキャッシュ(regex関数用)
			static map<string, gageas::regexp*> migemo_cache; // コンパイル済み正規表現のキャッシュ(migemo関数用)

			static char prev[2048]; // 連続数カウンタ用：前回のデータ
			static int repNum; // 連続数カウンタ用：前回までの連続数 
			static int counterIndex = 0; // 連続数カウンタ用：呼び出し回

			/* Multiple Valuesのいずれかの値にマッチ */
			void __cdecl MatchLine( sqlite3_context *ctx, int argc, sqlite3_value *argv[] )  {
				if(argc<2){
					sqlite3_result_int( ctx, 0 );
					return;
				}

				string wholeStr = string("\n").append((const char*)sqlite3_value_text(argv[0])).append("\n");

				while(argc-->1){
					string line = string("\n").append((const char*)sqlite3_value_text(argv[argc])).append("\n");
					if(wholeStr.find(line) != -1){
						sqlite3_result_int( ctx, 1 );
						return;
					}
				}

				sqlite3_result_int( ctx, 0 );
				return;
			};

			/* Multiple Valuesの全ての値にマッチ */
			void __cdecl MatchLineAll( sqlite3_context *ctx, int argc, sqlite3_value *argv[] )  {
				if(argc<2){
					sqlite3_result_int( ctx, 0 );
					return;
				}

				string wholeStr = string("\n").append((const char*)sqlite3_value_text(argv[0])).append("\n");

				while(argc-->1){
					string line = string("\n").append((const char*)sqlite3_value_text(argv[argc])).append("\n");
					if(wholeStr.find(line) == -1){
						sqlite3_result_int( ctx, 0 );
						return;
					}
				}

				sqlite3_result_int( ctx, 1 );
				return;
			};

			/* H2k6 LCMapUpper相当のSQL関数 */
			void __cdecl LCMapUpper( sqlite3_context *ctx, int argc, sqlite3_value *argv[] )  {
				if((argc != 1) || (argv[0] == NULL)){
					sqlite3_result_text16(ctx, L"", 0, SQLITE_TRANSIENT);
					return;
				}
				const wchar_t* src = (const wchar_t*)sqlite3_value_text16(argv[0]);
				size_t length = wcslen(src);
				wchar_t* buffer = new wchar_t[length+1];
				buffer[length] = 0;
				int maplen = LCMapString(GetUserDefaultLCID(), LCMAP_HALFWIDTH | LCMAP_HIRAGANA | LCMAP_UPPERCASE, src, length, buffer, length);
				if(maplen < 0){
					sqlite3_result_text16(ctx, L"", 0, SQLITE_TRANSIENT);
					delete [] buffer;
					return;
				}
				sqlite3_result_text16(ctx, buffer, maplen*2, SQLITE_TRANSIENT);
				delete [] buffer;
			};
			
			/* 同じ値の繰り返し数をカウント */
			void __cdecl __x_lutea_count_continuous( sqlite3_context *ctx, int argc, sqlite3_value *argv[] )  {
				const char* src = (const char*)sqlite3_value_text(argv[0]);
				if(strcmp(src, prev) != 0){
					strncpy(prev, src, sizeof(prev)-1);
					repNum = 0;
				}else{
					repNum++;
				}
				LuteaHelper::counter[counterIndex++] = repNum;
			};

			/* H2k6 current_timestamp64相当のSQL関数 */
			void __cdecl current_timestamp64( sqlite3_context *ctx, int argc, sqlite3_value *argv[] )  {
				static _timeb tv;
				_ftime64_s(&tv);
				sqlite3_result_int64( ctx, tv.time - (tv.timezone*60) );
			};

			/* 正規表現マッチのSQL関数 */
			void __cdecl regex( sqlite3_context *ctx, int argc, sqlite3_value *argv[] )  {
				if(argc != 2){
					sqlite3_result_int( ctx, 0 );
					return;
				}
				string pattern((const char*)sqlite3_value_text(argv[0]));
				if(regex_cache.size() > POOL_NUM){
					LuteaHelper::ClearRegexCache();
				}
				if(regex_cache.find(pattern) == regex_cache.end()){
					if(pattern[0] != '/'){
						sqlite3_result_int( ctx, 0 );
						return;
					}
					if((pattern.size() >= 2) && (pattern[pattern.size()-1] == '/')){
						gageas::regexp* re = new gageas::regexp("(?m)" + pattern.substr(1, pattern.size()-2), true);
						regex_cache[pattern] = re;
					}else if((pattern.size() >= 3) && (pattern[pattern.size()-2] == '/') && (pattern[pattern.size()-1] == 'i')) {
						gageas::regexp* re = new gageas::regexp("(?m)" + pattern.substr(1, pattern.size()-3), false);
						regex_cache[pattern] = re;
					}else{
						sqlite3_result_int( ctx, 0 );
						return;
					}
				}
				gageas::regexp* reppatern = regex_cache[pattern];
				const char* match = (const char*)sqlite3_value_text(argv[1]);
				sqlite3_result_int( ctx, reppatern->PartialMatch(match) ? 1 : 0);
			};

			/* migemoマッチのSQL関数 */
			void __cdecl migemo( sqlite3_context *ctx, int argc, sqlite3_value *argv[] )  {
				if(argc != 2){
					sqlite3_result_int( ctx, 0 );
					return;
				}
				const char* p_pattern = (const char*)sqlite3_value_text(argv[0]);
				const char* p_match = (const char*)sqlite3_value_text(argv[1]);

				if(strstr(p_match, p_pattern)){
					sqlite3_result_int( ctx, 1 );
					return;
				}

				string pattern(p_pattern);

				if(migemo_cache.size() > POOL_NUM){
					LuteaHelper::ClearMigemoCache();
				}
				if(migemo_cache.find(pattern) == migemo_cache.end()){
					const char* pptn = (const char*)sqlite3_value_text(argv[0]);
					System::IntPtr^ result = LuteaHelper::migemo_generator(gcnew System::IntPtr((int)pptn), strlen(pptn));
					if(result == System::IntPtr::Zero){
						sqlite3_result_int( ctx, 0 );
						return;
					}
					string migemore((const char*)(result->ToPointer()));
					gageas::regexp* re = new gageas::regexp(migemore, false);
					migemo_cache[pattern] = re;
					System::Runtime::InteropServices::Marshal::FreeHGlobal(*result);
				}
				gageas::regexp* reppatern = migemo_cache[pattern];
				sqlite3_result_int( ctx, reppatern->PartialMatch(p_match)?1:0);
			};

			/* SQLiteデータベースにSQL関数を登録する */
			void LuteaHelper::RegisterSQLiteUserFunctions(System::IntPtr^ _db, MigemoGenerator^ _migemo_generator){
				migemo_generator = _migemo_generator;
				sqlite3* db = (sqlite3*)_db->ToPointer();
				if(db == NULL)return;

				sqlite3_create_function(db, "current_timestamp64", 0, SQLITE_ANY, 0, current_timestamp64, NULL, NULL);
				sqlite3_create_function(db, "any", -1, SQLITE_UTF8, 0, MatchLine, NULL, NULL);
				sqlite3_create_function(db, "every", -1, SQLITE_UTF8, 0, MatchLineAll, NULL, NULL); // allという名前が使えなかった
				sqlite3_create_function(db, "regexp", 2, SQLITE_UTF8, 0, regex, NULL, NULL);
				sqlite3_create_function(db, "migemo", 2, SQLITE_UTF8, 0, migemo, NULL, NULL);
				sqlite3_create_function(db, "LCMapUpper", 1, SQLITE_UTF16, 0, LCMapUpper, NULL, NULL);
				sqlite3_create_function(db, "__x_lutea_count_continuous", 1, SQLITE_UTF8, 0, __x_lutea_count_continuous, NULL, NULL);
			};

			/* 連続数カウンタを初期化 */
			void LuteaHelper::ClearRepeatCount(int num){
				memset(prev, 0xff, 32); // 実際のアルバム名とstrcmpして絶対に一致しないようなゴミデータを書き込み
				prev[32] = 0x00; // NULL終端
				counterIndex = 0;
				if(LuteaHelper::counter != nullptr) delete LuteaHelper::counter;
				LuteaHelper::counter = gcnew cli::array<int>(num);
				ClearMigemoCache();
				ClearRegexCache();
			};

			void LuteaHelper::ClearMigemoCache(void){
				map<string, gageas::regexp*>::iterator p;
				for(p=migemo_cache.begin(); p!=migemo_cache.end(); p++)
				{
					delete(p->second);
				}
				migemo_cache.clear();
			};

			void LuteaHelper::ClearRegexCache(void){
				map<string, gageas::regexp*>::iterator p;
				for(p=regex_cache.begin(); p!=regex_cache.end(); p++)
				{
					delete(p->second);
				}
				regex_cache.clear();
			};
		};
	};
};