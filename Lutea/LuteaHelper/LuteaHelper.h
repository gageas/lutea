// LuteaHelper.h

#pragma once

namespace Gageas {
	namespace Lutea{
		namespace Core{
			public ref class LuteaHelper
			{
			public:
				static void ApplyGain(System::IntPtr^ destBuffer, unsigned int length, double gaindB);
			};
		};
	};
};

