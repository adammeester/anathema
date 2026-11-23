// Renders every ability icon SVG under Art/AbilityIcons/<Character>/ to a PNG at
// Assets/Resources/AbilityIcons/<Character>/<same name>.png, where the HUD loads it by ability class name
// (an EX that shares its base ability's component is named <Class>_EX).
//
//   cd Tools/IconRender && npm install && npm run render            (everything)
//   npm run render -- KhorneBerserker                               (one character)
import { Resvg } from "@resvg/resvg-js";
import { mkdirSync, readdirSync, readFileSync, statSync, writeFileSync } from "node:fs";
import { dirname, join, relative, resolve } from "node:path";
import { fileURLToPath } from "node:url";

const SIZE = 256; // px; the HUD draws icons at ~92px (1080p) and the tooltip a little larger
const root = resolve(dirname(fileURLToPath(import.meta.url)), "../..");
const src = join(root, "Art/AbilityIcons");
const dst = join(root, "Assets/Resources/AbilityIcons");
const only = process.argv[2];

let count = 0;
for (const character of readdirSync(src)) {
  if (only && character !== only) continue;
  const dir = join(src, character);
  if (!statSync(dir).isDirectory()) continue;
  for (const file of readdirSync(dir).filter(f => f.endsWith(".svg"))) {
    const svg = readFileSync(join(dir, file));
    const png = new Resvg(svg, { fitTo: { mode: "width", value: SIZE }, background: "rgba(0,0,0,0)" }).render().asPng();
    const out = join(dst, character, file.replace(/\.svg$/, ".png"));
    mkdirSync(dirname(out), { recursive: true });
    writeFileSync(out, png);
    console.log(relative(root, out));
    count++;
  }
}
console.log(`${count} icon(s) rendered.`);
