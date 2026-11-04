// Copies the flag-icons stylesheet and 4x3 flags next to the sign-up bundle as plain static files.
// (They go around Vite's Tailwind plugin, which can't process this third-party CSS.)
import { cpSync, mkdirSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";

const here = dirname(fileURLToPath(import.meta.url));
const src = resolve(here, "../node_modules/flag-icons");
const out = resolve(here, "../../Web/wwwroot/dist/signup/flag-icons");

mkdirSync(resolve(out, "css"), { recursive: true });
cpSync(resolve(src, "css/flag-icons.min.css"), resolve(out, "css/flag-icons.min.css"));
cpSync(resolve(src, "flags/4x3"), resolve(out, "flags/4x3"), { recursive: true });
console.log("flag-icons copied to", out);
