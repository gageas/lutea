// これは メイン DLL ファイルです。

#include <math.h>
#include "stdafx.h"

#include "LuteaHelper.h"
#include "bass.h"

typedef unsigned int uint;

namespace Gageas{
	namespace Lutea{
		namespace Core {
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