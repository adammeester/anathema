import { write, glow, embers, use, bolt, glowPath, rays, star, cog, flameRing, stunStars, chevrons, drops, f } from "./lib.mjs";

const A = "#f2a61a"; // Bullgryn
const dust = (x, y, s = 1) => `<g transform="translate(${x} ${y}) scale(${s})" fill="#8a7a6a" opacity="0.5"><circle cx="-8" cy="0" r="6"/><circle cx="2" cy="-3" r="7"/><circle cx="10" cy="1" r="5"/></g>`;
const link = (x, y, rot, w = 7, h = 4) => `<ellipse cx="${x}" cy="${y}" rx="${w}" ry="${h}" transform="rotate(${rot} ${x} ${y})" fill="none" stroke="#9a9a92" stroke-width="2.4"/>`;
write("Bullgryn", A, {
  // Slab Maul: the maul swung in a wide arc.
  SlabMaul: `${glow(0, -6, 44)}
${glowPath("M-44,18 A46,46 0 0 1 30,-34", A, "#fff0c8", 3)}
${use("maul", "translate(4 10) rotate(34) scale(0.95)")}
${embers([[34, -10], [-36, -16], [-20, -40]], "#ffcf7a")}`,

  // Shield Bash: the shield's rim slammed into whatever is in front.
  ShieldBash: `${glow(10, 0, 44)}
${use("slab", "translate(-6 4) rotate(-12) scale(1.05)")}
${star(20, -6, 16, 6, 8, "#ffcf7a", 'opacity="0.85"')}${star(20, -6, 8, 3, 8, "#ffffff")}
<g stroke="#ffcf7a" stroke-width="2.4" stroke-linecap="round"><line x1="32" y1="-22" x2="42" y2="-30"/><line x1="36" y1="-4" x2="48" y2="-4"/><line x1="32" y1="12" x2="42" y2="20"/></g>`,

  // Bull Charge: barrelling forward, unstoppable.
  BullCharge: `${glow(10, 0, 44)}
<g stroke="#d8c8a8" stroke-width="2.5" stroke-linecap="round" opacity="0.5"><line x1="-50" y1="-16" x2="-22" y2="-16"/><line x1="-54" y1="0" x2="-20" y2="0"/><line x1="-50" y1="16" x2="-22" y2="16"/></g>
${dust(-30, 34, 1.4)}
${use("slab", "translate(10 0) scale(1.05)")}
${chevrons(34, 0, 2, 7, 7, A, 0.9)}`,

  // Chain Haul: a hooked chain hurled out and dragged back.
  ChainHaul: `${glow(0, 0, 44)}
${[[-36, 30, -40], [-26, 22, -40], [-16, 14, -40], [-6, 6, -40], [4, -2, -40], [14, -10, -40]].map(([x, y, r], i) => link(x, y, i % 2 ? r + 90 : r)).join("")}
<path d="M22,-16 Q34,-28 30,-40 Q20,-44 16,-34" fill="none" stroke="url(#metal)" stroke-width="5" stroke-linecap="round"/>
<path d="M22,-16 Q34,-28 30,-40 Q20,-44 16,-34" fill="none" stroke="#0c0c0a" stroke-width="1" />
<polygon points="16,-34 10,-30 18,-28" fill="#c4c2ba" stroke="#0c0c0a" stroke-width="0.8"/>
<g fill="none" stroke="#ffcf7a" stroke-width="1.6" stroke-linecap="round" opacity="0.7"><path d="M36,-10 Q40,0 34,8"/><path d="M42,-14 Q48,0 40,12"/></g>`,

  // Brace: the slabshield raised, blocking everything from the front.
  Brace: `${glow(0, 0, 44)}
${use("slab", "translate(4 2) scale(1.25)")}
${use("round", "translate(-36 -20) rotate(15) scale(1.4)")}${use("round", "translate(-38 14) rotate(-10) scale(1.4)")}
${star(-22, -16, 6, 2, 6, "#ffcf7a")}${star(-24, 12, 6, 2, 6, "#ffcf7a")}`,

  // Ration Break: wolf down a ration and recover health.
  Brace_EX: `${glow(0, 4, 44)}
<path d="M-24,-6 L22,-16 L28,14 L-18,24 Z" fill="#7a6a4a" stroke="#2a2010" stroke-width="2"/>
<path d="M-24,-6 L22,-16 L24,-6 L-22,4 Z" fill="#9a8a5a"/>
<path d="M18,-15 Q26,-10 22,-2 Q30,4 26,12 L22,-15 Z" fill="#14100d"/>
<rect x="-14" y="2" width="22" height="9" fill="#d9c9a0" transform="rotate(-12 -3 6)"/>
<g stroke="#fff4cc" stroke-width="1.6" stroke-linecap="round" opacity="0.7" fill="none"><path d="M-10,-14 Q-14,-24 -8,-32"/><path d="M0,-18 Q-4,-28 2,-36"/></g>
<path d="M20,22 h6 v-6 h5 v6 h6 v5 h-6 v6 h-5 v-6 h-6 Z" fill="#8cd933" stroke="#1f2a0c" stroke-width="1"/>`,

  // Living Wall: the slabshield slammed into the ground as a wall.
  LivingWall: `${glow(0, 10, 46)}
<path d="M-46,34 L-30,30 L-20,36 L-6,30 L8,36 L22,30 L36,36 L50,32" fill="none" stroke="#2a1a0a" stroke-width="3"/>
<g stroke="#f2a61a" stroke-width="2"><rect x="-40" y="-16" width="26" height="44" rx="2" fill="#5a4a3a"/><rect x="-13" y="-24" width="26" height="52" rx="2" fill="#6e5c48"/><rect x="14" y="-16" width="26" height="44" rx="2" fill="#5a4a3a"/></g>
<g fill="#d9a441"><circle cx="-34" cy="-10" r="1.8"/><circle cx="-20" cy="-10" r="1.8"/><circle cx="-7" cy="-18" r="1.8"/><circle cx="7" cy="-18" r="1.8"/><circle cx="20" cy="-10" r="1.8"/><circle cx="34" cy="-10" r="1.8"/></g>
${dust(-30, 34, 1.2)}${dust(28, 34, 1.2)}
<g stroke="#ffcf7a" stroke-width="1.6" stroke-linecap="round" opacity="0.6"><line x1="-44" y1="-26" x2="-38" y2="-34"/><line x1="44" y1="-26" x2="38" y2="-34"/></g>`,
});

const T = "#4dbfff"; // Thousand Sons Sorcerer (blue, with gold sand)
const SAND = "#e3c27a";
const grains = (pts, c = SAND) => `<g fill="${c}">${pts.map(([x, y, r = 1.2]) => `<circle cx="${x}" cy="${y}" r="${r}"/>`).join("")}</g>`;
write("Sorcerer", T, {
  // Sands of Prospero: a bolt of enchanted sand.
  SandsOfProspero: `${glow(10, -10, 40)}
<path d="M-46,38 Q-16,14 8,-6" fill="none" stroke="${SAND}" stroke-width="12" opacity="0.3" filter="url(#soft)" stroke-linecap="round"/>
${grains([[-36, 30], [-30, 22, 1.4], [-22, 22], [-18, 12, 1.5], [-10, 10], [-6, 2, 1.6], [-26, 32, 0.9], [-14, 20, 1], [0, 4]])}
<circle cx="12" cy="-12" r="14" fill="${T}" opacity="0.5" filter="url(#soft)"/>
<circle cx="12" cy="-12" r="9" fill="${SAND}" stroke="#7a5418" stroke-width="1.2"/>
<path d="M12,-18 Q18,-12 12,-6 Q6,-12 12,-18 Z" fill="${T}"/>`,

  // Bolt of Change: a faster, harder bolt of changing fire.
  SandsOfProspero_EX: `${glow(8, -8, 44)}
<path d="M-46,40 Q-14,16 4,-2" fill="none" stroke="#c070ff" stroke-width="14" opacity="0.3" filter="url(#soft)" stroke-linecap="round"/>
<path d="M28,-30 Q44,-26 34,-10 Q26,4 8,4 Q-8,6 -20,20 Q-12,0 -4,-6 Q-14,-4 -20,2 Q-12,-16 6,-20 Q14,-34 28,-30 Z" fill="${T}" opacity="0.9"/>
<path d="M24,-24 Q32,-22 28,-12 Q22,-2 10,-2 Q2,0 -6,8 Q0,-6 8,-12 Q14,-24 24,-24 Z" fill="#c070ff"/>
<circle cx="20" cy="-16" r="5" fill="#ffffff"/><circle cx="20" cy="-16" r="2" fill="#0a2238"/>
${embers([[-24, 30], [36, -38], [-10, -28]], "#e0c0ff")}`,

  // Mending Sands: healing dust scattered over the ground.
  MendingSands: `${glow(0, 14, 44)}
<ellipse cx="0" cy="18" rx="40" ry="14" fill="${SAND}" fill-opacity="0.15" stroke="${SAND}" stroke-width="2"/>
<ellipse cx="0" cy="18" rx="28" ry="9" fill="none" stroke="${T}" stroke-width="1.2" opacity="0.7"/>
${grains([[-24, 16], [-12, 20, 1.5], [0, 14], [14, 20, 1.4], [24, 16], [-6, 24], [8, 26], [-18, 22, 0.9], [20, 24, 0.9]])}
<path d="M-6,-30 h12 v12 h12 v12 h-12 v12 h-12 v-12 h-12 v-12 h12 Z" fill="${SAND}" stroke="#5a4010" stroke-width="1.2" transform="translate(0 -2) scale(0.85)"/>
${grains([[-30, -10], [28, -16], [-20, -30], [24, -34]], "#fff4cc")}`,

  // Dust Form: come apart into sand and pour across the field.
  DustForm: `${glow(0, 0, 44)}
<path d="M-30,-36 Q-18,-38 -16,-24 Q-14,-10 -10,6 Q-20,4 -28,10 Q-32,-10 -34,-24 Q-36,-36 -30,-36 Z" fill="${T}" fill-opacity="0.35" stroke="${T}" stroke-width="1.2"/>
<circle cx="-25" cy="-30" r="5" fill="${T}" fill-opacity="0.5"/>
<path d="M-14,0 Q10,-6 20,10 Q30,24 46,20" fill="none" stroke="${SAND}" stroke-width="10" opacity="0.35" filter="url(#soft)" stroke-linecap="round"/>
${grains([[-10, -2, 1.6], [-4, -6], [2, -4, 1.4], [8, -2], [12, 4, 1.5], [16, 8], [20, 12, 1.4], [26, 18], [32, 20, 1.3], [38, 20], [44, 18, 1.1], [6, 2, 0.9], [18, 2, 0.9], [28, 12, 0.9], [-16, -6, 1.3], [-20, -12]])}`,

  // Kine Shield: a half-circle of telekinetic force turning blows aside.
  KineShield: `${glow(0, 0, 46)}
<path d="M-8,-36 A36,36 0 0 1 -8,36" fill="none" stroke="${T}" stroke-width="14" opacity="0.3" filter="url(#soft)"/>
<path d="M-8,-36 A36,36 0 0 1 -8,36" fill="none" stroke="${T}" stroke-width="5"/>
<path d="M-4,-30 A30,30 0 0 1 -4,30" fill="none" stroke="#d8f0ff" stroke-width="1.5"/>
${use("round", "translate(38 -14) rotate(200) scale(1.4)")}
<polyline points="32,-10 26,-4 36,2" fill="none" stroke="#ffcf7a" stroke-width="1.5"/>
${star(28, -6, 6, 2, 6, "#ffffff")}
<circle cx="-18" cy="0" r="6" fill="${SAND}" stroke="#7a5418"/>`,

  // Warp Rift: a tear in reality that slows and turns projectiles back.
  KineShield_EX: `${glow(0, 0, 46)}
<path d="M0,-44 L8,-26 L2,-16 L12,0 L4,12 L10,28 L0,44 L-6,26 L-2,12 L-12,-2 L-4,-14 L-10,-30 Z" fill="#c070ff" opacity="0.4" filter="url(#soft)"/>
<path d="M0,-40 L6,-24 L1,-14 L9,0 L3,12 L7,26 L0,40 L-4,24 L-1,12 L-9,-2 L-3,-14 L-7,-28 Z" fill="#05030a" stroke="${T}" stroke-width="2"/>
<path d="M0,-30 L3,-20 L0,-10 L5,0 L1,10" fill="none" stroke="#e0c0ff" stroke-width="1" opacity="0.8"/>
<g fill="none" stroke="${T}" stroke-width="1.5" opacity="0.6"><path d="M-36,-20 Q-20,-10 -14,-2"/><path d="M36,20 Q20,10 14,2"/></g>
${use("round", "translate(-34 22) rotate(160) scale(1.2)")}`,

  // Sinking Sands: an hourglass shatters and the ground swallows everyone.
  SinkingSands: `${glow(0, 16, 44)}
<ellipse cx="0" cy="24" rx="42" ry="14" fill="#7a5418" fill-opacity="0.5" stroke="${SAND}" stroke-width="2"/>
<g fill="none" stroke="${SAND}" stroke-width="1.5" opacity="0.8"><path d="M-30,24 A30,10 0 0 1 24,18"/><path d="M-18,26 A18,6 0 0 1 14,22"/><path d="M-6,26 A6,2.4 0 0 1 4,24"/></g>
${use("hourglass", "translate(0 -16) rotate(18) scale(1.1)")}
<g fill="#cfe6ff" opacity="0.6"><polygon points="-20,-30 -16,-36 -14,-28"/><polygon points="22,-6 28,-10 26,-2"/></g>`,

  // Dustglass Touch: foes hurled back and encased in dust-glass.
  DustglassTouch: `${glow(0, 0, 46)}
<g fill="#cfe6ff" fill-opacity="0.35" stroke="#e8f6ff" stroke-width="1.5"><polygon points="0,-38 18,-24 22,4 10,30 -12,30 -22,4 -18,-24"/></g>
<g fill="none" stroke="#e8f6ff" stroke-width="0.8" opacity="0.7"><polyline points="0,-38 2,-6 22,4"/><polyline points="2,-6 -12,30"/><polyline points="2,-6 -18,-24"/><polyline points="2,-6 10,30"/></g>
<path d="M-6,-14 Q0,-20 6,-14 Q4,-4 0,-2 Q-4,-4 -6,-14 Z" fill="#14100d" opacity="0.7"/>
<path d="M-46,-6 Q-30,-30 -6,-40" fill="none" stroke="${T}" stroke-width="3" opacity="0.7" stroke-linecap="round"/>
${grains([[-36, -24], [-28, -32], [-20, -38], [30, -30, 1.4], [32, 16]], "#fff4cc")}`,

  // Weaver of Fates: a teammate pulled out of time by golden threads.
  WeaverOfFates: `${glow(0, 0, 48)}
<g fill="none" stroke="${SAND}" stroke-width="1.6" opacity="0.85"><path d="M-44,-10 Q-20,-40 0,-14 Q20,12 44,-16"/><path d="M-44,14 Q-16,-20 4,8 Q24,36 44,10"/><path d="M-40,32 Q-10,4 8,26"/></g>
<circle r="18" fill="${T}" opacity="0.35" filter="url(#soft)"/>
<path d="M0,-16 Q12,-4 0,10 Q-12,-4 0,-16 Z" fill="${T}"/>
<circle cx="0" cy="-3" r="4" fill="#ffffff"/><circle cx="0" cy="-3" r="1.8" fill="#0a2238"/>
<path d="M-14,4 Q0,20 14,4" fill="none" stroke="${SAND}" stroke-width="2.5"/>
${grains([[-30, -30], [30, 28], [36, -32], [-34, 26]], "#fff4cc")}`,
});

// An ornate power sword pointing right, hilt at the origin: a crackling red power field around a steel blade,
// a gilded winged crossguard, a blood-drop gem and a red leather grip.
const powerSword = (t) => `<g transform="${t}">
<path d="M1,-6.5 L44,-5 L56,0 L44,5 L1,6.5 Z" fill="#ff3a24" opacity="0.45" filter="url(#soft)"/>
<path d="M2,-4.2 L43,-3.4 L52,0 L43,3.4 L2,4.2 Z" fill="url(#metal)" stroke="#2a0a06" stroke-width="1"/>
<path d="M4,-0.4 L42,-0.3 L48,0 L42,0.3 L4,0.4 Z" fill="#ffe8e0"/>
<path d="M3,-4.2 L43,-3.4 L52,0" fill="none" stroke="#ff7a6a" stroke-width="0.9" opacity="0.9"/>
<g fill="none" stroke="#ffd0c8" stroke-width="0.8" stroke-linecap="round"><polyline points="10,-5 14,-8 17,-5 21,-7"/><polyline points="28,5 31,8 35,5 38,7"/><polyline points="40,-4 43,-7 46,-4"/></g>
<path d="M0,-4 Q-4,-14 6,-20 Q12,-22 14,-18 Q6,-16 6,-10 Q8,-12 11,-12 Q5,-9 4,-4 Z" fill="url(#gold)" stroke="#3a2a10" stroke-width="0.8"/>
<path d="M0,4 Q-4,14 6,20 Q12,22 14,18 Q6,16 6,10 Q8,12 11,12 Q5,9 4,4 Z" fill="url(#gold)" stroke="#3a2a10" stroke-width="0.8"/>
<rect x="-3" y="-6" width="6" height="12" rx="1.5" fill="url(#gold)" stroke="#3a2a10" stroke-width="0.8"/>
<path d="M0,-3 Q2.5,0 0,3 Q-2.5,0 0,-3 Z" fill="#c4161c" stroke="#5a0606" stroke-width="0.5"/>
<rect x="-15" y="-2.6" width="12" height="5.2" rx="1" fill="#6a0c0c" stroke="#2a0404" stroke-width="0.8"/>
<g stroke="#e3b862" stroke-width="1"><line x1="-12" y1="-2.6" x2="-12" y2="2.6"/><line x1="-8" y1="-2.6" x2="-8" y2="2.6"/></g>
<circle cx="-17.5" cy="0" r="3.4" fill="url(#gold)" stroke="#3a2a10" stroke-width="0.8"/><circle cx="-17.5" cy="0" r="1.4" fill="#c4161c"/>
</g>`;
const R = "#e61f1f"; // Sanguinary Priest
const chalice = (x, y, s = 1, rot = 0) => `<g transform="translate(${x} ${y}) rotate(${rot}) scale(${s})">
<path d="M-26,-16 L26,-16 Q24,12 0,18 Q-24,12 -26,-16 Z" fill="url(#gold)" stroke="#2a1a08" stroke-width="2"/>
<path d="M-20,-12 Q-18,6 -4,12" fill="none" stroke="#f2d48a" stroke-width="1.2" opacity="0.6"/>
<ellipse cy="-16" rx="26" ry="5" fill="#5a0606" stroke="#2a1a08" stroke-width="1.5"/><ellipse cy="-16.5" rx="20" ry="3" fill="#a81010"/>
<circle cy="2" r="4.5" fill="#6a0808" stroke="#e3b862" stroke-width="1.5"/>
<rect x="-4" y="18" width="8" height="16" fill="url(#gold)" stroke="#2a1a08" stroke-width="1.5"/>
<path d="M-18,44 L18,44 L11,34 L-11,34 Z" fill="url(#gold)" stroke="#2a1a08" stroke-width="1.5"/></g>`;
write("SanguinaryPriest", R, {
  // Power Sword (class Chainsword): alternating cuts, each sending a stream of blood to the wounded.
  Chainsword: `${glow(0, 0, 44)}
${glowPath("M-40,-24 Q0,-44 40,-20", R, "#ffb0a0", 2.4)}
${powerSword("translate(-24 26) rotate(-42) scale(1.1)")}
<path d="M10,-10 Q30,-4 30,20 Q28,34 14,38" fill="none" stroke="#a81010" stroke-width="2.5" stroke-dasharray="1 4" stroke-linecap="round"/>
${drops([[14, 40, 1.2], [30, 8, 0.8]], "#c4161c")}`,

  // Sanguine Lunge: dash forward and run them through.
  SanguineLunge: `${glow(10, 0, 44)}
<g stroke="#d8c0b0" stroke-width="2.4" stroke-linecap="round" opacity="0.45"><line x1="-50" y1="-10" x2="-26" y2="-10"/><line x1="-54" y1="4" x2="-24" y2="4"/><line x1="-50" y1="18" x2="-28" y2="18"/></g>
${powerSword("translate(-18 4) rotate(-8) scale(1.15)")}
${star(38, -2, 12, 4, 8, R, 'opacity="0.85"')}${star(38, -2, 6, 2, 8, "#ffd0c8")}
${drops([[30, 20, 1], [42, 16, 0.8]], "#c4161c")}`,

  // Jump Pack: a burning descent onto the cursor.
  JumpPackDescent: `${glow(0, 10, 46)}
<g fill="#ff7a1a" opacity="0.8"><path d="M-10,-44 Q-6,-26 -12,-12 Q-16,-24 -10,-44 Z"/><path d="M10,-44 Q14,-26 8,-12 Q4,-24 10,-44 Z"/></g>
<path d="M-12,-14 L12,-14 L8,8 L-8,8 Z" fill="#6a0c0c" stroke="#e3b862" stroke-width="1.5"/>
${use("wing", "translate(-8 -10) scale(0.7)", 'fill="#e8d8b0" stroke="#5a4a30"')}${use("wing", "translate(8 -10) scale(-0.7 0.7)", 'fill="#e8d8b0" stroke="#5a4a30"')}
<ellipse cx="0" cy="30" rx="38" ry="10" fill="none" stroke="${R}" stroke-width="3"/>
<ellipse cx="0" cy="30" rx="26" ry="6.5" fill="none" stroke="#ffb070" stroke-width="1.5"/>
<g stroke="#ffcf7a" stroke-width="2" stroke-linecap="round"><line x1="-30" y1="20" x2="-40" y2="12"/><line x1="30" y1="20" x2="40" y2="12"/></g>`,

  // Chalice Offering: the chalice raised to heal everyone around.
  ChaliceOffering: `${glow(0, -6, 44)}
<g stroke="#d9a441" stroke-width="2" opacity="0.25"><line x1="0" y1="-10" x2="-40" y2="-40"/><line x1="0" y1="-10" x2="40" y2="-40"/><line x1="0" y1="-10" x2="-48" y2="-12"/><line x1="0" y1="-10" x2="48" y2="-12"/></g>
${use("wing", "translate(-24 -16) scale(0.75)", 'fill="#8a7a6a" stroke="#3a3020" opacity="0.8"')}${use("wing", "translate(24 -16) scale(-0.75 0.75)", 'fill="#8a7a6a" stroke="#3a3020" opacity="0.8"')}
${chalice(0, 0, 1)}
<path d="M-23,-15 Q-25,-4 -21,2 Q-17,-4 -19,-15 Z" fill="#8a0c0c"/>
<path d="M0,-44 Q8,-32 0,-27 Q-8,-32 0,-44 Z" fill="#b01212"/>`,

  // Red Thirst: the chalice poured out for one.
  ChaliceOffering_EX: `${glow(10, 10, 44)}
${chalice(-14, -6, 0.85, -60)}
<path d="M-4,-20 Q14,-14 16,6 Q18,20 14,30" fill="none" stroke="#a81010" stroke-width="5" stroke-linecap="round"/>
<path d="M-4,-20 Q14,-14 16,6" fill="none" stroke="#ff6a5a" stroke-width="1.2" opacity="0.7"/>
${drops([[14, 38, 2.2]], "#c4161c")}
<circle cx="12" cy="34" r="2" fill="#ffb0a8"/>`,

  // Sanguine Parry: the blow turned aside, the wounded mended.
  SanguineParry: `${glow(0, 0, 46)}
${powerSword("translate(-26 26) rotate(-60) scale(1.05)")}
${star(-2, -10, 14, 5, 8, "#ffcf7a", 'opacity="0.9"')}${star(-2, -10, 7, 2.5, 8, "#ffffff")}
<g stroke="#ffcf7a" stroke-width="1.6" stroke-linecap="round"><line x1="-16" y1="-24" x2="-24" y2="-32"/><line x1="12" y1="-24" x2="18" y2="-34"/></g>
${drops([[28, 22, 1.8]], "#c4161c")}
<path d="M34,4 h4 v-4 h3 v4 h4 v3 h-4 v4 h-3 v-4 h-4 Z" fill="#ffd0c8"/>`,

  // Wings of the Angel: golden wings unfold before the crash down.
  WingsOfTheAngel: `${glow(0, -4, 48)}
<rect x="-6" y="-50" width="12" height="70" fill="#ffd973" opacity="0.25" filter="url(#soft)"/>
${use("wing", "translate(-4 0) scale(1.1)", 'fill="url(#gold)" stroke="#5a3a10"')}${use("wing", "translate(4 0) scale(-1.1 1.1)", 'fill="url(#gold)" stroke="#5a3a10"')}
<path d="M-8,-10 L8,-10 L6,14 L-6,14 Z" fill="#8a0c0c" stroke="#e3b862" stroke-width="1.5"/>
<circle cx="0" cy="-18" r="6" fill="#d8c8a8" stroke="#3a3020" stroke-width="1"/>
<ellipse cx="0" cy="-27" rx="9" ry="2.8" fill="none" stroke="#ffd973" stroke-width="2"/>
<ellipse cx="0" cy="34" rx="34" ry="8" fill="none" stroke="${R}" stroke-width="2.5"/>
${embers([[-30, -30], [30, -34], [-36, 10], [36, 8]], "#ffcf7a")}`,
});
