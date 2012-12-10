/* 本プロジェクトをコンパイルするには，re2winのソースコードを取得し，ヘッダファイルをインクルードパスに入れてください */

#define POOL_NUM (16)
#define NOMINMAX 

#include <math.h>
#include <sys/types.h>
#include <sys/timeb.h>
#include <string>
#include <map>
#include "stdafx.h"

#include "LuteaHelper.h"
#include "bass.h"
#include "sqlite3.h"
#include "re2.h"

typedef unsigned int uint;

using namespace std;


namespace Gageas{
	namespace Lutea{
		namespace Core {
			static map<string, re2::RE2*> regex_cache; // コンパイル済み正規表現のキャッシュ(regex関数用)
			static map<string, re2::RE2*> migemo_cache; // コンパイル済み正規表現のキャッシュ(migemo関数用)

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
				delete [] buffer;
				sqlite3_result_text16(ctx, buffer, maplen*2, SQLITE_TRANSIENT);
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

					map<string, re2::RE2*>::iterator p;

					for(p=regex_cache.begin(); p!=regex_cache.end(); p++)
					{
						delete(p->second);
					}
					regex_cache.clear();
				}
				if(regex_cache.find(pattern) == regex_cache.end()){
					if(pattern[0] != '/'){
						sqlite3_result_int( ctx, 0 );
						return;
					}
					if((pattern.size() >= 2) && (pattern[pattern.size()-1] == '/')){
						re2::RE2::Options ops;
						ops.set_case_sensitive(true);
						re2::RE2* re = new re2::RE2(pattern.substr(1, pattern.size()-2), ops);
						regex_cache[pattern] = re;
					}else if((pattern.size() >= 3) && (pattern[pattern.size()-2] == '/') && (pattern[pattern.size()-1] == 'i')) {
						re2::RE2::Options ops;
						ops.set_case_sensitive(false);
						re2::RE2* re = new re2::RE2(pattern.substr(1, pattern.size()-3), ops);
						regex_cache[pattern] = re;
					}else{
						sqlite3_result_int( ctx, 0 );
						return;
					}
				}
				re2::RE2* reppatern = regex_cache[pattern];
				const char* match = (const char*)sqlite3_value_text(argv[1]);
				sqlite3_result_int( ctx, (re2::RE2::PartialMatch(match, *reppatern)?1:0) );
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
					map<string, re2::RE2*>::iterator p;
					for(p=migemo_cache.begin(); p!=migemo_cache.end(); p++)
					{
						delete(p->second);
					}
					migemo_cache.clear();
				}
				if(migemo_cache.find(pattern) == migemo_cache.end()){
					const char* pptn = (const char*)sqlite3_value_text(argv[0]);
					System::IntPtr^ result = LuteaHelper::migemo_generator(gcnew System::IntPtr((int)pptn), strlen(pptn));
					if(result == System::IntPtr::Zero){
						sqlite3_result_int( ctx, 0 );
						return;
					}
					string migemore((const char*)(result->ToPointer()));
					re2::RE2::Options ops;
					ops.set_case_sensitive(false);
					re2::RE2* re = new re2::RE2(migemore, ops);
					migemo_cache[pattern] = re;
					System::Runtime::InteropServices::Marshal::FreeHGlobal(*result);
				}
				re2::RE2* reppatern = migemo_cache[pattern];
				sqlite3_result_int( ctx, (re2::RE2::PartialMatch(p_match, *reppatern)?1:0) );
			};

			/* SQLiteデータベースにSQL関数を登録する */
			void LuteaHelper::RegisterSQLiteUserFunctions(System::IntPtr^ _db, MigemoGenerator^ _migemo_generator){
				migemo_generator = _migemo_generator;
				sqlite3* db = (sqlite3*)_db->ToPointer();
				if(db == NULL)return;

				sqlite3_create_function(db, "current_timestamp64", 0, SQLITE_ANY, 0, current_timestamp64, NULL, NULL);
				sqlite3_create_function(db, "regexp", 2, SQLITE_UTF8, 0, regex, NULL, NULL);
				sqlite3_create_function(db, "migemo", 2, SQLITE_UTF8, 0, migemo, NULL, NULL);
				sqlite3_create_function(db, "LCMapUpper", 1, SQLITE_UTF16, 0, LCMapUpper, NULL, NULL);
			};

			/* オーディオサンプルデータ(floatの配列)にリプレイゲインを適用する */
			void LuteaHelper::ApplyGain(System::IntPtr^ destBuffer, unsigned int length, double gaindB){
				double gain_l = pow(10.0, gaindB / 20.0);
				float*dest = (float*)(destBuffer->ToPointer());
				for (int i = 0, l = (int)(length / sizeof(float)); i < l; i++)
				{
					dest[i] *= (float)gain_l;
				}
			};
		};
	};
};