import { write, glow, embers, use, bolt, glowPath, rays, star, cog, flameRing, stunStars, chevrons, drops, f } from "./lib.mjs";

const H = "#ffd973"; // Hospitaller
// The Hospitaller's holy icon: a gilded sun disc on a short haft.
const icon = (x, y, s = 1, rot = 0) => `<g transform="translate(${x} ${y}) rotate(${rot}) scale(${s})">
<rect x="-2" y="6" width="4" height="22" fill="#5a3a1a" stroke="#1a0e06" stroke-width="0.8"/>
${star(0, -4, 13, 8, 12, "url(#gold)", 'stroke="#3a2a10" stroke-width="0.8"')}
<circle cx="0" cy="-4" r="6.5" fill="#fff4cc" stroke="#7a5418" stroke-width="1"/></g>`;
write("Hospitaller", H, {
  // Sanctified Tether: a beam of light latched onto a target.
  SanctifiedTether: `${glow(0, 0, 46)}
${icon(-30, 26, 0.9, -30)}
${glowPath("M-24,18 Q0,-6 32,-28", H, "#ffffff", 3)}
<circle cx="32" cy="-28" r="10" fill="none" stroke="#fff4cc" stroke-width="2"/><circle cx="32" cy="-28" r="16" fill="none" stroke="${H}" stroke-width="1" opacity="0.6"/>
${embers([[-4, -4], [10, -18], [18, -6], [-12, 8]], "#fff4cc")}`,

  // Penance: a darker beam of judgement on a marked enemy.
  SanctifiedTether_EX: `${glow(0, 0, 46, 0.6)}
${icon(-30, 26, 0.9, -30)}
${glowPath("M-24,18 Q0,-6 26,-22", "#b0302a", "#ffb0a0", 3)}
<g fill="none" stroke="#8a8a84" stroke-width="2">${[0, 1, 2, 3].map(i => `<ellipse cx="${-14 + i * 10}" cy="${10 - i * 8}" rx="4" ry="2.4" transform="rotate(-38 ${-14 + i * 10} ${10 - i * 8})"/>`).join("")}</g>
${use("skull", "translate(28 -24) scale(0.5)")}
<circle cx="28" cy="-26" r="20" fill="none" stroke="#c4161c" stroke-width="2"/><line x1="14" y1="-40" x2="42" y2="-12" stroke="#c4161c" stroke-width="2"/>`,

  // Sanctus Pulse: blessed ground that pulses once, sharing its healing.
  SanctusPulse: `${glow(0, 14, 44)}
<ellipse cx="0" cy="18" rx="42" ry="15" fill="${H}" fill-opacity="0.12" stroke="${H}" stroke-width="2.5"/>
<ellipse cx="0" cy="18" rx="30" ry="10" fill="none" stroke="#fff4cc" stroke-width="1.5" opacity="0.8"/>
<ellipse cx="0" cy="18" rx="16" ry="5.5" fill="none" stroke="#fff4cc" stroke-width="1" opacity="0.6"/>
<rect x="-5" y="-42" width="10" height="60" fill="${H}" opacity="0.3" filter="url(#soft)"/>
<path d="M-5,-24 h10 v10 h10 v10 h-10 v10 h-10 v-10 h-10 v-10 h10 Z" fill="#fff4cc" stroke="#7a5418" stroke-width="1" transform="translate(0 -4) scale(0.9)"/>
${embers([[-24, -10, 1.4], [22, -18, 1.3], [-12, -32], [30, 0]], "#fff4cc")}`,

  // Blinding Radiance: the icon thrust forward in a blinding cone.
  BlindingRadiance: `${glow(10, 0, 46)}
<path d="M-20,8 L52,-34 L52,46 Z" fill="${H}" opacity="0.22"/>
<path d="M-20,8 L52,-14 L52,28 Z" fill="#fff4cc" opacity="0.3"/>
${rays(-20, 8, 14, 60, 0, H)}
<g stroke="#fff4cc" stroke-width="1.6" stroke-linecap="round" opacity="0.8"><line x1="0" y1="2" x2="40" y2="-20"/><line x1="2" y1="8" x2="46" y2="8"/><line x1="0" y1="14" x2="40" y2="34"/></g>
${icon(-22, 10, 1.15, 70)}`,

  // Dove Strike: the circling doves dive on their prey.
  DoveStrike: `${glow(0, 0, 44)}
<g stroke="#fff4cc" stroke-width="1.4" stroke-linecap="round" opacity="0.5"><line x1="-40" y1="-30" x2="-20" y2="-14"/><line x1="-6" y1="-44" x2="8" y2="-26"/><line x1="-44" y1="-6" x2="-28" y2="6"/></g>
${use("dove", "translate(-6 -4) rotate(38) scale(1.5)", 'fill="#f4efe2"')}
${use("dove", "translate(18 -24) rotate(52) scale(1.05)", 'fill="#f4efe2"')}
${use("dove", "translate(-20 18) rotate(30) scale(0.95)", 'fill="#f4efe2"')}
${star(24, 30, 9, 3, 6, H)}`,

  // Golden Dove: gilded doves that send their targets running.
  DoveStrike_EX: `${glow(0, 0, 46)}
<circle cx="0" cy="-2" r="26" fill="${H}" opacity="0.25" filter="url(#blur4)"/>
${rays(0, -2, 26, 42, 16, H, 2, 0.45)}
${use("dove", "translate(0 0) rotate(-8) scale(2)", 'fill="#e8b84a"')}
<circle cx="0" cy="-2" r="34" fill="none" stroke="#fff4cc" stroke-width="1.2" stroke-dasharray="2 4" opacity="0.7"/>`,

  // Martyr's Grace: the hit is caught in a halo of light and struck back.
  MartyrsGrace: `${glow(0, 0, 46)}
<ellipse cx="0" cy="-20" rx="22" ry="7" fill="none" stroke="${H}" stroke-width="4"/>
<ellipse cx="0" cy="-20" rx="22" ry="7" fill="none" stroke="#fff4cc" stroke-width="1.5"/>
<path d="M0,-6 Q-20,-14 -18,6 Q-16,22 0,34 Q16,22 18,6 Q20,-14 0,-6 Z" fill="url(#gold)" stroke="#3a2a10" stroke-width="1.5"/>
<path d="M0,2 Q-10,-2 -10,8 Q-8,18 0,24" fill="none" stroke="#fff4cc" stroke-width="1.5" opacity="0.7"/>
${bolt("22,0 30,-6 28,-12 40,-18", H, "#ffffff", 1.3)}${bolt("-22,0 -30,-6 -28,-12 -40,-18", H, "#ffffff", 1.3)}
${embers([[30, 24], [-30, 26], [0, 44]], "#fff4cc")}`,

  // Absolution: a wave of light that washes every affliction away.
  Absolution: `${glow(0, 10, 46)}
<g fill="none" stroke-linecap="round"><path d="M-44,36 Q0,-6 44,36" stroke="${H}" stroke-width="4" opacity="0.8"/><path d="M-40,20 Q0,-22 40,20" stroke="#fff4cc" stroke-width="2.5" opacity="0.7"/><path d="M-34,4 Q0,-34 34,4" stroke="${H}" stroke-width="1.5" opacity="0.5"/></g>
<g fill="none" stroke="#8a8a84" stroke-width="2.2"><ellipse cx="-18" cy="34" rx="5" ry="3" transform="rotate(30 -18 34)"/><ellipse cx="-10" cy="38" rx="5" ry="3" transform="rotate(-20 -10 38)"/><ellipse cx="14" cy="36" rx="5" ry="3" transform="rotate(-30 14 36)"/></g>
<line x1="-4" y1="30" x2="6" y2="40" stroke="#fff4cc" stroke-width="2"/>
${icon(0, -8, 1.05)}
${embers([[-30, -20], [28, -24], [-14, -40], [16, -40]], "#fff4cc")}`,

  // Martyrdom: holy fire binds every ally to you by burning threads.
  Martyrdom: `${glow(0, 0, 48)}
<g stroke="#ff9a3a" stroke-width="1.6" opacity="0.85"><line x1="0" y1="4" x2="-36" y2="-24"/><line x1="0" y1="4" x2="38" y2="-18"/><line x1="0" y1="4" x2="-30" y2="36"/><line x1="0" y1="4" x2="32" y2="34"/></g>
<g fill="${H}" stroke="#7a5418" stroke-width="1"><circle cx="-36" cy="-24" r="4"/><circle cx="38" cy="-18" r="4"/><circle cx="-30" cy="36" r="4"/><circle cx="32" cy="34" r="4"/></g>
<path d="M0,-30 Q14,-12 12,4 Q10,20 0,22 Q-10,20 -12,4 Q-14,-12 0,-30 Z" fill="#ff7a1a" opacity="0.9"/>
<path d="M0,-16 Q8,-4 6,8 Q4,16 0,16 Q-4,16 -6,8 Q-8,-4 0,-16 Z" fill="#ffd973"/>
<path d="M0,-4 Q3,4 2,10 Q0,12 -2,10 Q-3,4 0,-4 Z" fill="#ffffff"/>
${embers([[14, -26], [-16, -30], [6, -40]], "#ffcf7a")}`,
});

const M = "#73ff4d"; // Mandrake
// A Mandrake's face emerging from shadow: gaunt blue-black skin, glowing eye slits and runes, a needle-toothed grin,
// shadow tendrils around it and its right side melting into darkness. `fade` (0..1) is how far the darkness eats in.
const mandrakeFace = (fade = 0.55) => `<defs><linearGradient id="veilfade" x1="0" y1="0" x2="1" y2="0"><stop offset="0" stop-color="#020402" stop-opacity="0"/><stop offset="${1 - fade}" stop-color="#020402" stop-opacity="0"/><stop offset="1" stop-color="#020402" stop-opacity="0.95"/></linearGradient></defs>
<g fill="#050806" stroke="${M}" stroke-width="0.8" stroke-opacity="0.5"><path d="M-20,-30 Q-40,-40 -46,-20 Q-36,-30 -28,-20 Z"/><path d="M20,-30 Q42,-42 48,-18 Q36,-30 28,-20 Z"/><path d="M-22,10 Q-44,14 -48,34 Q-36,20 -20,22 Z"/><path d="M22,10 Q44,16 46,38 Q34,22 20,22 Z"/><path d="M-6,34 Q-10,46 -2,52 Q0,42 6,36 Z"/></g>
<g fill="#0c1216" stroke="#000" stroke-width="1"><polygon points="-16,-34 -24,-52 -8,-38"/><polygon points="-4,-38 -2,-56 6,-38"/><polygon points="8,-36 20,-52 16,-32"/></g>
<path d="M0,-40 Q22,-38 24,-12 Q24,8 14,22 Q6,34 0,38 Q-6,34 -14,22 Q-24,8 -24,-12 Q-22,-38 0,-40 Z" fill="#1a2430" stroke="#000" stroke-width="1.5"/>
<path d="M-20,-4 Q-16,8 -10,12 Q-14,2 -12,-4 Z" fill="#3a4c5c" opacity="0.8"/><path d="M20,-4 Q16,8 10,12 Q14,2 12,-4 Z" fill="#3a4c5c" opacity="0.8"/>
<path d="M-6,-36 Q0,-38 6,-36 Q2,-30 0,-20 Q-2,-30 -6,-36 Z" fill="#2c3a48" opacity="0.8"/>
<path d="M-22,-12 L-4,-8 L-2,-12 L-20,-18 Z" fill="#05080a"/><path d="M22,-12 L4,-8 L2,-12 L20,-18 Z" fill="#05080a"/>
<polygon points="-19,-9 -6,-5 -16,-3" fill="${M}" opacity="0.6" filter="url(#soft)"/><polygon points="19,-9 6,-5 16,-3" fill="${M}" opacity="0.6" filter="url(#soft)"/>
<polygon points="-19,-9 -6,-5 -16,-3" fill="#d8ffc8"/><polygon points="19,-9 6,-5 16,-3" fill="#d8ffc8"/>
<path d="M0,-6 L-2,6 L0,7 L2,6 Z" fill="#0a1014"/>
<path d="M-13,13 Q0,24 13,13 Q0,30 -13,13 Z" fill="#05080a" stroke="#000" stroke-width="0.8"/>
<polyline points="-11,14 -9,18 -7,15 -5,20 -3,16 -1,21 1,16 3,20 5,15 7,18 9,14 11,15" fill="none" stroke="#d8dcc8" stroke-width="1" stroke-linejoin="bevel"/>
<g stroke="${M}" stroke-width="1.1" stroke-linecap="round" fill="none"><polyline points="0,-32 0,-24 -3,-21"/><line x1="3" y1="-28" x2="0" y2="-26"/><polyline points="-18,2 -15,6 -18,10"/><polyline points="18,2 15,6 18,10"/></g>
<path d="M-26,-42 L26,-42 L26,40 L-26,40 Z" fill="url(#veilfade)"/>`;

const pool = (x, y, rx, ry) => `<ellipse cx="${x}" cy="${y}" rx="${rx}" ry="${ry}" fill="#020402" stroke="${M}" stroke-width="2"/><ellipse cx="${x}" cy="${y}" rx="${rx * 0.65}" ry="${ry * 0.6}" fill="none" stroke="${M}" stroke-width="1" opacity="0.5"/>`;
const slash = (d, c = M) => `<path d="${d}" fill="${c}" opacity="0.35" filter="url(#soft)"/><path d="${d}" fill="${c}"/>`;
write("Mandrake", M, {
  // Glimmersteel Blade: a three-strike chain of cuts.
  GlimmersteelBlade: `${glow(0, 0, 44)}
${slash("M-40,-20 Q0,-44 40,-26 Q0,-36 -40,-20 Z")}${slash("M-42,4 Q0,-16 44,-2 Q0,-10 -42,4 Z", "#b8ff9a")}${slash("M-40,30 Q0,6 42,26 Q0,16 -40,30 Z")}
${use("kblade", "translate(6 6) rotate(30) scale(1.15)")}`,

  // Balefire: a bolt of green witch-fire.
  BalefireBolt: `${glow(10, -10, 40)}
<path d="M-44,40 Q-14,14 4,-4" fill="none" stroke="${M}" stroke-width="12" opacity="0.25" filter="url(#soft)" stroke-linecap="round"/>
<path d="M22,-26 Q40,-30 34,-14 Q30,0 14,4 Q-4,8 -16,22 Q-6,2 -2,-6 Q-12,-2 -18,6 Q-12,-14 2,-20 Q10,-34 22,-26 Z" fill="#2aa81a" opacity="0.85"/>
<path d="M20,-20 Q30,-22 26,-12 Q22,-4 12,-2 Q2,0 -4,8 Q2,-6 8,-12 Q12,-22 20,-20 Z" fill="${M}"/>
<circle cx="18" cy="-14" r="5" fill="#eaffd8"/>
${embers([[-24, 26], [-30, 34, 0.9], [36, -36], [-12, -30]], "#b8ff9a")}`,

  // Shadow Jump: into one shadow and out of another.
  ShadowJump: `${glow(0, 0, 44)}
${pool(-26, 30, 18, 6)}${pool(26, -8, 18, 6)}
<path d="M-26,24 Q-10,-40 22,-14" fill="none" stroke="${M}" stroke-width="2.2" stroke-dasharray="4 4"/>
<path d="M-30,24 Q-28,10 -24,4 M-22,26 Q-20,14 -16,8" fill="none" stroke="#3a5a3a" stroke-width="2" opacity="0.8"/>
<path d="M20,-14 Q22,-30 28,-36 M28,-12 Q30,-26 34,-32" fill="none" stroke="${M}" stroke-width="1.6" opacity="0.7"/>
${star(26, -10, 6, 2, 4, "#eaffd8")}`,

  // Umbral Return: jump away, leaving a mark to snap back to.
  ShadowJump_EX: `${glow(0, 0, 44)}
${pool(-24, 26, 18, 6)}${pool(24, -12, 16, 5.5)}
<path d="M-20,20 Q-6,-36 20,-18" fill="none" stroke="${M}" stroke-width="2.2" stroke-dasharray="4 4"/>
<path d="M26,-4 Q20,34 -14,30" fill="none" stroke="#b8ff9a" stroke-width="2.5"/>
<polygon points="-20,30 -10,24 -10,36" fill="#b8ff9a"/>
${star(-24, 24, 8, 3, 4, M)}`,

  // Shadow Veil: a Mandrake's face sinking back into the shadows.
  ShadowVeil: `${glow(0, 0, 44, 0.6)}
${mandrakeFace(0.6)}
${embers([[-34, 6], [36, -4], [-28, -34], [30, 30]], "#b8ff9a")}`,

  // Shed the Light: fading into shadow, throwing off every affliction.
  ShadowVeil_EX: `${glow(0, 0, 44, 0.6)}
${mandrakeFace(0.45)}
<g fill="none" stroke="#8a8a84" stroke-width="2.2"><ellipse cx="-38" cy="-6" rx="5" ry="3" transform="rotate(30 -38 -6)"/><ellipse cx="-44" cy="2" rx="5" ry="3" transform="rotate(-20 -44 2)"/><ellipse cx="40" cy="12" rx="5" ry="3" transform="rotate(-40 40 12)"/></g>
<g fill="#ff8a3a"><path d="M-36,28 Q-32,20 -30,28 Q-32,34 -36,28 Z"/></g><g fill="#9adf3a"><circle cx="38" cy="-26" r="3"/></g>
<g stroke="#eaffd8" stroke-width="1.4" stroke-linecap="round" opacity="0.8"><line x1="-28" y1="-4" x2="-34" y2="-6"/><line x1="28" y1="8" x2="34" y2="10"/><line x1="-26" y1="22" x2="-32" y2="26"/><line x1="28" y1="-18" x2="34" y2="-22"/></g>`,

  // Umbral Riposte: the hit meets only shadow, and the blade answers.
  UmbralRiposte: `${glow(0, 0, 46)}
${star(-6, 2, 30, 12, 10, "#0a140a", `stroke="${M}" stroke-width="1.5"`)}
${use("kblade", "translate(8 2) rotate(-35) scale(1.1)")}
${star(-14, -12, 8, 3, 6, "#eaffd8")}
<g stroke="#eaffd8" stroke-width="1.4" stroke-linecap="round"><line x1="-22" y1="-20" x2="-30" y2="-28"/><line x1="-10" y1="-22" x2="-8" y2="-32"/><line x1="-24" y1="-8" x2="-34" y2="-6"/></g>`,

  // Shadow Rend: a short, vicious, stunning cut.
  ShadowRend: `${glow(0, 4, 44)}
${slash("M-42,30 Q-6,-10 40,-34 Q4,-2 -42,30 Z")}
<path d="M-38,26 Q-4,-6 34,-28" fill="none" stroke="#eaffd8" stroke-width="1.5"/>
${stunStars(4, -30, 18, "#b8ff9a")}
${use("kblade", "translate(22 18) rotate(60) scale(0.85)")}`,

  // Shadow Cross: a ghostly X torn in two huge slashes.
  ShadowCross: `${glow(0, 0, 48)}
${slash("M-40,-40 L-30,-40 L40,38 L30,40 Z")}${slash("M40,-40 L30,-40 L-40,38 L-30,40 Z")}
<g stroke="#eaffd8" stroke-width="1.5"><line x1="-35" y1="-38" x2="35" y2="38"/><line x1="35" y1="-38" x2="-35" y2="38"/></g>
<circle r="7" fill="#eaffd8" opacity="0.8" filter="url(#soft)"/>
${embers([[-40, 0], [40, 2], [0, -42], [2, 42]], "#b8ff9a")}`,
});
