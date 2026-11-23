import { write, glow, embers, use, bolt, glowPath, rays, star, cog, flameRing, stunStars, chevrons, drops, f } from "./lib.mjs";

const G = "#8cd933"; // Plague Surgeon
const cloud = (cx, cy, s, fill, op = 1) => `<g transform="translate(${cx} ${cy}) scale(${s})" fill="${fill}" opacity="${op}"><circle cx="-16" cy="2" r="14"/><circle cx="0" cy="-8" r="18"/><circle cx="18" cy="2" r="14"/><circle cx="2" cy="8" r="15"/></g>`;
write("PlagueSurgeon", G, {
  // Plague Spit: a glob of plague flung along the aim.
  PlagueSpit: `${glow(8, -8, 40)}
<g fill="#6a9a1a" opacity="0.7"><circle cx="-30" cy="30" r="3"/><circle cx="-20" cy="22" r="4"/><circle cx="-36" cy="40" r="2"/><circle cx="-10" cy="14" r="5"/></g>
<path d="M14,-26 Q32,-24 30,-8 Q30,6 14,8 Q-2,10 -4,-4 Q-10,-2 -14,4 Q-10,-10 -2,-14 Q2,-26 14,-26 Z" fill="#7ab82a" stroke="#1f2a0c" stroke-width="1.5"/>
<path d="M6,-20 Q14,-24 22,-18" fill="none" stroke="#e0ffb0" stroke-width="2" opacity="0.7" stroke-linecap="round"/>
<g fill="#4a6a14"><circle cx="10" cy="-6" r="3"/><circle cx="20" cy="-12" r="2"/><circle cx="18" cy="0" r="1.6"/></g>
${drops([[12, 18, 1], [24, 14, 0.8]], "#7ab82a")}`,

  // Pestilent Fly: a plague fly loosed to latch on and feed.
  PestilentFly: `${glow(4, 0, 40)}
<g fill="none" stroke="${G}" stroke-width="1.5" stroke-linecap="round" opacity="0.6"><path d="M-44,24 Q-30,10 -20,18 Q-10,26 -4,14"/><path d="M-40,36 Q-24,26 -14,32"/></g>
${use("fly", "translate(10 -4) rotate(40) scale(2.6)")}
${embers([[30, -30], [-20, -30], [36, 20]], "#c4f060")}`,

  // Brood Fly: a bloated fly that splits, a second fly leaping off it.
  PestilentFly_EX: `${glow(0, 0, 44)}
${use("fly", "translate(-10 8) rotate(30) scale(2.8)")}
<path d="M4,-6 Q18,-34 30,-22" fill="none" stroke="${G}" stroke-width="1.8" stroke-dasharray="3 3"/>
${use("fly", "translate(32 -22) rotate(60) scale(1.5)")}
<g fill="#9adf3a" opacity="0.8"><circle cx="2" cy="-10" r="2.4"/><circle cx="8" cy="-16" r="1.6"/></g>`,

  // Pox Flask: a flask of rot lobbed to shatter in a splash.
  PoxFlask: `${glow(0, 20, 40)}
<path d="M-40,20 Q-20,-46 14,-20" fill="none" stroke="#6a9a1a" stroke-width="1.8" stroke-dasharray="4 4" opacity="0.8"/>
<ellipse cx="10" cy="32" rx="30" ry="9" fill="#5a8a14" opacity="0.7"/>
${star(10, 28, 18, 7, 9, "#9adf3a", 'transform="translate(10 28) scale(1 0.55) translate(-10 -28)"')}
${use("flask", "translate(10 -18) rotate(35) scale(1.25)")}
<g fill="#9adf3a"><circle cx="-6" cy="16" r="2.6"/><circle cx="28" cy="14" r="2.2"/><circle cx="18" cy="8" r="1.6"/></g>`,

  // Restorative Cloud: a lingering cloud that heals allies inside it.
  PlagueCloud: `${glow(0, 4, 46)}
${cloud(0, 6, 1.15, "#4a6a1a")}${cloud(-2, 0, 0.9, "#7aa82a", 0.85)}
<g fill="#ffe08a"><path d="M-4,-14 h8 v-8 h6 v8 h8 v6 h-8 v8 h-6 v-8 h-8 Z" stroke="#5a4010" stroke-width="1" transform="translate(-6 8) scale(0.9)"/></g>
${embers([[-24, -26, 1.6], [22, -30, 1.4], [30, -12, 1.2], [-32, -6, 1.2], [6, -38, 1.4]], "#e8ff9a")}`,

  // Virulent Cloud: a full-strength cloud of poison with a grinning skull in it.
  PlagueCloud_EX: `${glow(0, 4, 46)}
${cloud(0, 4, 1.25, "#3a5a12")}${cloud(0, 0, 1.0, "#5a8a1a", 0.9)}
${use("skull", "translate(0 0) scale(0.55)")}
<g fill="${G}"><circle cx="-5.5" cy="-1.5" r="2"/><circle cx="5.5" cy="-1.5" r="2"/></g>
${drops([[-20, 34, 1], [6, 38, 1.1], [24, 32, 0.9]], "#7ab82a")}`,

  // Winged Escape: insect wings tear out and carry you away.
  WingedEscape: `${glow(0, 0, 46)}
<g stroke="#3a4020" stroke-width="1"><path d="M-2,0 Q-30,-40 -46,-30 Q-40,-8 -4,6 Z" fill="#d4dcb0" fill-opacity="0.55"/><path d="M2,0 Q30,-40 46,-30 Q40,-8 4,6 Z" fill="#d4dcb0" fill-opacity="0.55"/><path d="M-2,6 Q-30,20 -36,34 Q-14,30 0,10 Z" fill="#d4dcb0" fill-opacity="0.45"/><path d="M2,6 Q30,20 36,34 Q14,30 0,10 Z" fill="#d4dcb0" fill-opacity="0.45"/></g>
<g fill="none" stroke="#5a6a30" stroke-width="0.7" opacity="0.8"><path d="M-4,2 Q-24,-22 -40,-28"/><path d="M-6,4 Q-26,-8 -40,-16"/><path d="M4,2 Q24,-22 40,-28"/><path d="M6,4 Q26,-8 40,-16"/></g>
<ellipse cx="0" cy="4" rx="5" ry="12" fill="#3a4a14" stroke="#141a06" stroke-width="1"/>
${chevrons(-6, -38, 1, 0, 6, "#c4f060", 0.8).replace(/polyline points="[^"]*"/, 'polyline points="-8,-36 0,-44 8,-36"')}${chevrons(-6, -30, 1, 0, 6, "#c4f060", 0.5).replace(/polyline points="[^"]*"/, 'polyline points="-8,-28 0,-36 8,-28"')}`,

  // Pestilent Burst: a bloated fly bursting in a spray of rot.
  PestilentBurst: `${glow(0, 0, 48)}
${star(0, 0, 40, 16, 11, "#4a7a12", 'opacity="0.8"')}${star(0, 0, 28, 12, 11, "#8cd933")}
<circle r="12" fill="#c4f060" opacity="0.8" filter="url(#soft)"/>
${use("fly", "translate(0 2) scale(1.7)")}
<g fill="#7ab82a"><circle cx="-38" cy="-22" r="3"/><circle cx="36" cy="26" r="3.4"/><circle cx="30" cy="-34" r="2.4"/><circle cx="-30" cy="34" r="2.6"/></g>`,

  // Ward of Plague: a shield of plague-light with Nurgle's mark, flies circling.
  WardOfPlague: `${glow(0, 0, 46)}
<circle r="32" fill="${G}" fill-opacity="0.15" stroke="${G}" stroke-width="3"/>
<circle r="26" fill="none" stroke="#c4f060" stroke-width="1" stroke-dasharray="2 4" opacity="0.8"/>
<g fill="none" stroke="#c4f060" stroke-width="3"><circle cx="0" cy="-7" r="8.5"/><circle cx="-7.8" cy="5" r="8.5"/><circle cx="7.8" cy="5" r="8.5"/></g>
<circle cx="0" cy="1" r="2.6" fill="#e8ff9a"/>
${use("fly", "translate(-34 -24) rotate(-60) scale(0.9)")}${use("fly", "translate(36 -16) rotate(70) scale(0.8)")}${use("fly", "translate(10 38) rotate(170) scale(0.85)")}`,
});

const C = "#59e6f2"; // Skitarii Ranger
const tracer = (x1, y1, x2, y2, c = C, w = 2) => `<line x1="${x1}" y1="${y1}" x2="${x2}" y2="${y2}" stroke="${c}" stroke-width="${w * 3}" opacity="0.3" filter="url(#soft)" stroke-linecap="round"/><line x1="${x1}" y1="${y1}" x2="${x2}" y2="${y2}" stroke="${c}" stroke-width="${w}" stroke-linecap="round"/><line x1="${x1}" y1="${y1}" x2="${x2}" y2="${y2}" stroke="#ffffff" stroke-width="${w * 0.4}" stroke-linecap="round"/>`;
const caltrop = (x, y, s = 1) => `<g transform="translate(${x} ${y}) scale(${s})" stroke="#0c0c0a" stroke-width="0.8" fill="url(#metal)"><polygon points="0,-7 2,0 -2,0"/><polygon points="-6,4 0,-1 1,2"/><polygon points="6,4 0,-1 -1,2"/><circle r="1.6" fill="#3a3a36"/></g>`;
write("SkitariiRanger", C, {
  // Galvanic Shot: fast galvanic rounds from the arquebus.
  GalvanicShot: `${glow(0, 0, 44)}
${tracer(-44, 26, 8, -6)}${tracer(-40, 40, 20, 4, C, 1.6)}${tracer(-46, 10, -4, -16, C, 1.3)}
${use("round", "translate(12 -8) rotate(-32) scale(2)")}${use("round", "translate(24 2) rotate(-32) scale(1.6)")}
${bolt("20,-18 26,-24 22,-28 30,-32", C, "#ffffff", 0.9)}
${embers([[36, -36], [-20, -34], [40, 14]], "#bff8ff")}`,

  // Arc Round: a round wreathed in galvanic arcs that leap off it.
  GalvanicShot_EX: `${glow(0, 0, 46)}
${tracer(-46, 30, -2, 2)}
<circle cx="4" cy="-2" r="12" fill="${C}" opacity="0.4" filter="url(#soft)"/>
${use("round", "translate(4 -2) rotate(-32) scale(2.2)")}
${bolt("8,-8 18,-20 14,-24 26,-36", C, "#ffffff", 1.3)}${bolt("12,2 26,4 28,12 40,14", C, "#ffffff", 1.3)}${bolt("-2,-10 -10,-24 -4,-28 -12,-40", C, "#ffffff", 1.1)}
<g fill="${C}"><circle cx="26" cy="-36" r="3.4"/><circle cx="40" cy="14" r="3.4"/><circle cx="-12" cy="-40" r="3"/></g>`,

  // Transuranic Shot: a huge piercing shot, charged at the muzzle.
  TransuranicShot: `${glow(-26, 26, 36)}
<g fill="none" stroke="${C}"><circle cx="-30" cy="30" r="8" stroke-width="2"/><circle cx="-30" cy="30" r="14" stroke-width="1.5" opacity="0.7"/><circle cx="-30" cy="30" r="20" stroke-width="1" opacity="0.4"/></g>
<line x1="-30" y1="30" x2="52" y2="-52" stroke="#7affc0" stroke-width="16" opacity="0.25" filter="url(#soft)"/>
<line x1="-30" y1="30" x2="52" y2="-52" stroke="#7affc0" stroke-width="7"/>
<line x1="-30" y1="30" x2="52" y2="-52" stroke="#ffffff" stroke-width="2.5"/>
<circle cx="-30" cy="30" r="6" fill="#ffffff"/>
${embers([[10, 10, 1.2], [-10, -20], [30, -8]], "#c8fff0")}`,

  // Overcharge Round: an instant shot fired hot, no wind-up.
  TransuranicShot_EX: `${glow(0, 0, 46)}
<line x1="-46" y1="46" x2="52" y2="-52" stroke="#ffb04a" stroke-width="18" opacity="0.3" filter="url(#soft)"/>
<line x1="-46" y1="46" x2="52" y2="-52" stroke="#ffe08a" stroke-width="6"/>
<line x1="-46" y1="46" x2="52" y2="-52" stroke="#ffffff" stroke-width="2"/>
${star(-8, 8, 18, 6, 8, "#ffb04a", 'opacity="0.85"')}${star(-8, 8, 10, 4, 8, "#fff2c0")}
<g fill="#f2c31a" stroke="#1a1408" stroke-width="1"><polygon points="20,18 34,42 6,42"/></g><rect x="19" y="26" width="2.4" height="8" fill="#1a1408"/><rect x="19" y="36" width="2.4" height="2.6" fill="#1a1408"/>`,

  // Shrapnel Canister: a burst of whirling metal that shreds and slows.
  ShrapnelField: `${glow(0, 4, 46)}
<circle cx="0" cy="4" r="34" fill="none" stroke="${C}" stroke-width="1.5" stroke-dasharray="3 5" opacity="0.6"/>
<g fill="url(#metal)" stroke="#0c0c0a" stroke-width="0.8">${[0, 1, 2, 3, 4, 5, 6, 7, 8, 9].map(i => { const a = i * 0.63, r = 16 + (i % 3) * 8; return `<polygon points="0,-4 3,3 -3,2" transform="translate(${f(Math.cos(a) * r)} ${f(4 + Math.sin(a) * r)}) rotate(${i * 47})"/>`; }).join("")}</g>
<g fill="none" stroke="#bff8ff" stroke-width="1.2" opacity="0.6"><path d="M-22,-14 A26,26 0 0 1 20,-18"/><path d="M22,22 A26,26 0 0 1 -20,26"/></g>
<rect x="-6" y="-4" width="12" height="16" rx="3" fill="#4a4a46" stroke="#0c0c0a" stroke-width="1.2"/><rect x="-6" y="1" width="12" height="3" fill="${C}"/>`,

  // Scattershot: a short-range shotgun blast in a cone.
  ShrapnelField_EX: `${glow(-10, 10, 44)}
<path d="M-36,34 L40,-20 A46,46 0 0 1 14,40 Z" fill="${C}" fill-opacity="0.15"/>
${star(-34, 32, 14, 5, 8, "#ffb04a", 'opacity="0.9"')}${star(-34, 32, 7, 3, 8, "#fff2c0")}
<g stroke="#bff8ff" stroke-width="1.4" stroke-linecap="round" opacity="0.85"><line x1="-24" y1="24" x2="30" y2="-12"/><line x1="-22" y1="28" x2="34" y2="6"/><line x1="-24" y1="32" x2="26" y2="24"/><line x1="-26" y1="36" x2="14" y2="40"/></g>
<g fill="#e8ffff">${[[34, -14], [38, 4], [30, 24], [18, 40], [24, -2], [12, 16]].map(([x, y]) => `<circle cx="${x}" cy="${y}" r="2.2"/>`).join("")}</g>`,

  // Disengage: a servo-assisted leap straight back.
  Disengage: `${glow(0, 0, 44)}
<path d="M30,30 Q10,-40 -30,-4" fill="none" stroke="${C}" stroke-width="2.5" stroke-dasharray="5 4"/>
<polygon points="-36,0 -26,-12 -22,2" fill="${C}"/>
<g stroke="#bff8ff" stroke-width="2" stroke-linecap="round" opacity="0.6"><line x1="22" y1="36" x2="40" y2="36"/><line x1="26" y1="42" x2="44" y2="42"/></g>
${cog(30, 30, 10, 8, "#5a5a56")}
${chevrons(-8, 22, 3, -8, 5, C, 0.7)}`,

  // Caltrop Burst: leap away, scattering caltrops behind.
  Disengage_EX: `${glow(0, 0, 44)}
<path d="M34,10 Q6,-44 -32,-12" fill="none" stroke="${C}" stroke-width="2.5" stroke-dasharray="5 4"/>
<polygon points="-38,-8 -28,-20 -24,-6" fill="${C}"/>
<ellipse cx="4" cy="30" rx="40" ry="10" fill="${C}" fill-opacity="0.1"/>
${caltrop(-20, 26, 1.4)}${caltrop(0, 32, 1.6)}${caltrop(20, 24, 1.3)}${caltrop(-6, 18, 1.1)}${caltrop(30, 34, 1.2)}`,

  // Counter: a cog-ward catches the hit and a shot goes straight back.
  RangerCounter: `${glow(0, 0, 46)}
${cog(-8, 6, 28, 12, "#4a4a46", "#0c0c0a", 'fill-opacity="0.9"')}
<circle cx="-8" cy="6" r="22" fill="none" stroke="${C}" stroke-width="2"/>
${tracer(0, -2, 46, -36, "#7affc0", 3)}
${star(-6, 4, 9, 3, 6, "#ffffff")}
${star(-30, -16, 8, 3, 6, "#ffcf7a")}<g stroke="#ffcf7a" stroke-width="1.4" stroke-linecap="round"><line x1="-38" y1="-24" x2="-44" y2="-30"/><line x1="-26" y1="-28" x2="-24" y2="-36"/></g>`,

  // Noospheric Snare: a sanctified cog-mine woven into the noosphere.
  NoosphericSnare: `${glow(0, 10, 44)}
<ellipse cx="0" cy="16" rx="40" ry="14" fill="none" stroke="${C}" stroke-width="1.5" stroke-dasharray="2 3"/>
<ellipse cx="0" cy="16" rx="28" ry="9" fill="none" stroke="${C}" stroke-width="1" opacity="0.6"/>
<g transform="translate(0 12) scale(1 0.6)">${cog(0, 0, 20, 10, "#5a5a56")}</g>
<circle cx="0" cy="12" r="4" fill="#ff3a24"/><circle cx="0" cy="12" r="8" fill="#ff3a24" opacity="0.4" filter="url(#soft)"/>
<g fill="${C}" font-family="monospace" opacity="0.8">${[[-30, -20], [-16, -30], [2, -36], [18, -28], [30, -16]].map(([x, y], i) => `<rect x="${x}" y="${y}" width="${i % 2 ? 2 : 5}" height="2"/><rect x="${x}" y="${y + 5}" width="${i % 2 ? 5 : 2}" height="2"/>`).join("")}</g>
<g stroke="${C}" stroke-width="0.8" opacity="0.5"><line x1="0" y1="8" x2="-30" y2="-16"/><line x1="0" y1="8" x2="2" y2="-30"/><line x1="0" y1="8" x2="30" y2="-12"/></g>`,

  // Culling Volley: four enormous shots in a row.
  CullingVolley: `${glow(0, 0, 48)}
${tracer(-50, 24, 30, -36, "#7affc0", 3)}${tracer(-44, 34, 36, -26, "#7affc0", 3)}${tracer(-38, 44, 42, -16, "#7affc0", 3)}${tracer(-56, 14, 24, -46, "#7affc0", 3)}
${star(30, -36, 7, 2.5, 6, "#ffffff")}${star(36, -26, 7, 2.5, 6, "#ffffff")}${star(42, -16, 7, 2.5, 6, "#ffffff")}${star(24, -46, 7, 2.5, 6, "#ffffff")}
<g fill="#c8fff0" font-family="monospace"><rect x="-40" y="-36" width="4" height="4"/><rect x="-32" y="-36" width="4" height="4"/><rect x="-24" y="-36" width="4" height="4"/><rect x="-16" y="-36" width="4" height="4" opacity="0.4"/></g>`,
});
