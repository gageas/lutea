#pragma once
#include <memory>
#include <string>

namespace re2 {
	class RE2;
}

namespace gageas {
	class regexp {
	public:
		std::unique_ptr<re2::RE2> inst;
		regexp(const std::string& pattern, bool case_sensitive);
		~regexp();
		bool PartialMatch(const char* match);

	};
}