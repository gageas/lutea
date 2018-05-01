#include "Stdafx.h"
#include "regexp.h"
#include "re2/re2.h"

gageas::regexp::regexp(std::string pattern, bool case_sensitive) {
	re2::RE2::Options ops;
	ops.set_case_sensitive(case_sensitive);
	inst.reset(new re2::RE2(pattern, ops));
}

gageas::regexp::~regexp() {
	inst.release();
}

bool gageas::regexp::PartialMatch(const char* match) {
	return re2::RE2::PartialMatch(match, *inst);
}