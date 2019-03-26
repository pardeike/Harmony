# Execution Flow

Patching a method does not override any previous patches that other users of Harmony apply to the same method. Instead, prefix and postfix patches are executed in a prioritised way. Prefix patches can return a boolean that, if false, terminates prefixes and skips the execution of the original method. In contrast, all postfixes are executed all the time.

Execution of prefixes and postfixes can explained best with the following pseudo code:

	run = true
	result = null;

	if (run) run = Prefix1(...)
	if (run) run = Prefix2(...)
	// ...

	if (run) result = Original(...)

	Postfix1(...)
	Postfix2(...)
	// ...

	return result