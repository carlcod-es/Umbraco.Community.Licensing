// Prints the runner platform line: node tools/release/platform.mjs. Used by every workflow (ADR-0008).
import { platformLine } from './check.mjs';

console.log(platformLine());
