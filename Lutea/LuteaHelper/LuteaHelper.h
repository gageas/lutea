// LuteaHelper.h
#pragma once

#include "sqlite3.h"

namespace Gageas {
	namespace Lutea{
		namespace Core{
			public ref class LuteaHelper
			{
			public:
				delegate System::IntPtr^ MigemoGenerator(System::IntPtr^ utf8_src, System::Int32 length);
				static void RegisterSQLiteUserFunctions(System::IntPtr^ _db, MigemoGenerator^ migemoGenerator);
				static void ClearRepeatCount(int);
				static LuteaHelper::MigemoGenerator^ migemo_generator;
				static cli::array<int>^counter = nullptr;
				static void ClearMigemoCache(void);
				static void ClearRegexCache(void);
			};
		};
	};
};

