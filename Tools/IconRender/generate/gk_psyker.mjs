import { write, glow, embers, use, bolt, glowPath, rays, star, flameRing, stunStars, chevrons, drops, f } from "./lib.mjs";

const B = "#73bfff"; // Grey Knight
write("GreyKnight", B, {
  // Storm Bolter: a burst of bolter rounds streaking from a muzzle flash.
  BasicAttack: `${glow(0, 0, 46, 0.6)}
${star(-30, 28, 15, 5, 8, "#ffb04a", 'opacity="0.9"')}${star(-30, 28, 8, 3, 8, "#fff2c0")}
<g stroke="#ffcf7a" stroke-linecap="round" opacity="0.55"><line x1="-24" y1="20" x2="-4" y2="2" stroke-width="2"/><line x1="-14" y1="24" x2="10" y2="2" stroke-width="1.5"/><line x1="-26" y1="10" x2="-2" y2="-12" stroke-width="1.5"/></g>
${use("round", "translate(-4 2) rotate(-42) scale(2)")}${use("round", "translate(18 -4) rotate(-42) scale(2)")}${use("round", "translate(10 -26) rotate(-42) scale(2)")}
${embers([[24, 18], [-8, -30], [36, -8, 0.9]], "#ffcf7a")}`,

  // Smite: a psychic lance fired from the halberd's tip.
  Smite: `${glow(0, 0, 46)}
<g fill="none" stroke="#3a5a7a" stroke-width="1.2" opacity="0.6"><circle cx="-34" cy="34" r="16"/><circle cx="-34" cy="34" r="11"/><line x1="-50" y1="34" x2="-18" y2="34"/><line x1="-34" y1="18" x2="-34" y2="50"/></g>
<line x1="-36" y1="36" x2="46" y2="-46" stroke="${B}" stroke-width="18" stroke-linecap="round" opacity="0.18"/>
<line x1="-36" y1="36" x2="46" y2="-46" stroke="${B}" stroke-width="9" stroke-linecap="round" opacity="0.4"/>
<polygon points="-40,30 -30,40 50,-50" fill="#9ed2ff"/>
<line x1="-34" y1="34" x2="48" y2="-48" stroke="#ffffff" stroke-width="2"/>
<g fill="none" stroke="#cfe8ff" stroke-width="1.5"><polyline points="-6,-2 -16,-10 -8,-14 -20,-24"/><polyline points="14,10 26,14 20,22 32,28"/><polyline points="-18,22 -26,12 -32,16"/></g>
<circle cx="-34" cy="34" r="7" fill="#cfe8ff" opacity="0.7"/>
${embers([[20, -34], [-10, -38], [36, 0]], "#bfe2ff")}`,

  // Halberd Sweep: the halberd carving a wide frontal arc.
  HalberdSweep: `${glow(0, -10, 46)}
${glowPath("M-44,10 A46,46 0 0 1 44,10", B, "#e8f4ff", 3)}
<path d="M-36,14 A38,38 0 0 1 36,14" fill="none" stroke="#bfe2ff" stroke-width="1" opacity="0.6"/>
${use("halberd", "translate(8 10) rotate(48) scale(0.95)")}
${embers([[-30, -30], [34, -26], [-40, 4]], "#bfe2ff")}`,

  // Teleport Strike: vanish in a fading sigil, reappear in a crack of psychic force.
  TeleportStrike: `${glow(14, -10, 40)}
<ellipse cx="-28" cy="30" rx="16" ry="6" fill="none" stroke="${B}" stroke-width="1.5" stroke-dasharray="3 3" opacity="0.6"/>
<g fill="#9ed2ff" opacity="0.6"><rect x="-31" y="10" width="2" height="2"/><rect x="-25" y="4" width="2" height="2"/><rect x="-28" y="18" width="1.6" height="1.6"/><rect x="-21" y="14" width="1.6" height="1.6"/></g>
<path d="M-26,24 Q-20,-20 8,-12" fill="none" stroke="${B}" stroke-width="2" stroke-dasharray="4 4" opacity="0.7"/>
${star(14, -10, 26, 8, 8, B, 'opacity="0.55" filter="url(#soft)"')}${star(14, -10, 18, 5, 8, "#cfe8ff")}
<circle cx="14" cy="-10" r="22" fill="none" stroke="#e8f4ff" stroke-width="1.5" opacity="0.8"/>
<circle cx="14" cy="-10" r="30" fill="none" stroke="${B}" stroke-width="1" opacity="0.5"/>
<circle cx="14" cy="-10" r="5" fill="#ffffff"/>
${bolt("30,-24 36,-30 34,-36 42,-40", B, "#ffffff", 1.2)}${bolt("0,4 -4,12 2,14 -2,22", B, "#ffffff", 1.2)}`,

  // Counter: a psychic ward catching a blow, halberd ready behind it.
  InterceptorCounter: `${glow(0, 0, 46)}
${use("halberd", "translate(14 6) rotate(28) scale(0.85)")}
<circle r="28" fill="${B}" fill-opacity="0.16" stroke="${B}" stroke-width="2.5"/>
<circle r="22" fill="none" stroke="#cfe8ff" stroke-width="1" opacity="0.7"/>
${star(0, 0, 12, 4, 8, "none", 'stroke="#cfe8ff" stroke-width="1.2" opacity="0.8"')}
${star(-20, -20, 12, 3, 6, "#ffffff", 'opacity="0.9"')}
<g stroke="#ffffff" stroke-width="1.4" stroke-linecap="round"><line x1="-30" y1="-30" x2="-38" y2="-38"/><line x1="-14" y1="-32" x2="-12" y2="-42"/><line x1="-32" y1="-14" x2="-42" y2="-12"/></g>`,

  // Consecrate: a warding sigil inscribed on the ground, about to detonate.
  Consecrate: `${glow(0, 10, 44)}
<rect x="-6" y="-46" width="12" height="60" fill="${B}" opacity="0.25" filter="url(#soft)"/>
<ellipse cx="0" cy="16" rx="42" ry="16" fill="${B}" fill-opacity="0.12" stroke="${B}" stroke-width="2.5"/>
<ellipse cx="0" cy="16" rx="32" ry="12" fill="none" stroke="#cfe8ff" stroke-width="1" stroke-dasharray="5 3"/>
<g transform="translate(0 16) scale(1 0.38)">${star(0, 0, 26, 10, 8, "none", 'stroke="#e8f4ff" stroke-width="3"')}</g>
<ellipse cx="0" cy="16" rx="6" ry="2.4" fill="#ffffff"/>
${embers([[-14, -10], [10, -24], [4, -38], [-6, -30]], "#cfe8ff")}`,

  // Sanctum: a psychic dome turning projectiles back.
  Consecrate_EX: `${glow(0, 6, 46)}
<ellipse cx="0" cy="24" rx="40" ry="10" fill="none" stroke="${B}" stroke-width="1.5" opacity="0.7"/>
<path d="M-40,24 A40,40 0 0 1 40,24" fill="${B}" fill-opacity="0.18" stroke="${B}" stroke-width="2.5"/>
<g fill="none" stroke="#cfe8ff" stroke-width="0.8" opacity="0.5"><path d="M-28,24 Q-26,-6 0,-16 Q26,-6 28,24"/><path d="M-14,24 Q-12,-10 0,-16 Q12,-10 14,24"/><path d="M-38,10 Q0,0 38,10"/></g>
<path d="M-30,0 A34,34 0 0 1 4,-14" fill="none" stroke="#ffffff" stroke-width="1.5" opacity="0.7"/>
${use("round", "translate(-34 -30) rotate(35) scale(1.2)")}
<polyline points="-26,-24 -18,-18 -30,-12" fill="none" stroke="#ffcf7a" stroke-width="1.5" opacity="0.8"/>
${use("round", "translate(34 -28) rotate(155) scale(1.2)")}
${star(24, -22, 6, 2, 6, "#ffffff")}`,

  // Purgatus: an expanding ring of warp-fire.
  Purgatus: `${glow(0, 0, 48)}
${flameRing(0, 0, 30, 18, 12, B, 0.5)}${flameRing(0, 0, 30, 18, 7, "#cfe8ff", 0.9)}
<circle r="30" fill="none" stroke="#ffffff" stroke-width="2"/>
<circle r="18" fill="none" stroke="${B}" stroke-width="1.2" opacity="0.6"/>
${star(0, 0, 9, 3, 4, "#ffffff")}
<g stroke="#cfe8ff" stroke-width="1.6" stroke-linecap="round" opacity="0.8"><line x1="0" y1="-20" x2="0" y2="-12"/><line x1="0" y1="12" x2="0" y2="20"/><line x1="-20" y1="0" x2="-12" y2="0"/><line x1="12" y1="0" x2="20" y2="0"/></g>
${embers([[44, -10], [-42, 18], [20, 44], [-18, -46]], "#cfe8ff")}`,
});

const P = "#b380ff"; // Psyker
write("Psyker", P, {
  // Warp Bolt: a crackling orb of warp energy.
  WarpBolt: `${glow(10, -10, 40)}
<path d="M-44,40 Q-10,12 10,-10" fill="none" stroke="${P}" stroke-width="14" opacity="0.25" filter="url(#soft)" stroke-linecap="round"/>
<path d="M-40,34 Q-12,10 8,-8" fill="none" stroke="${P}" stroke-width="3" opacity="0.6" stroke-linecap="round"/>
<path d="M-30,38 Q-6,18 10,-6" fill="none" stroke="#e8d8ff" stroke-width="1" opacity="0.6"/>
<circle cx="12" cy="-12" r="15" fill="${P}" opacity="0.6" filter="url(#soft)"/>
<circle cx="12" cy="-12" r="10" fill="#d9c2ff"/><circle cx="12" cy="-12" r="5.5" fill="#ffffff"/>
${bolt("22,-20 30,-22 28,-28 36,-32", P, "#ffffff", 1.1)}${bolt("4,-22 0,-30 6,-32 2,-40", P, "#ffffff", 1.1)}${bolt("22,-4 30,2 26,6 34,10", P, "#ffffff", 1.1)}`,

  // Chain Lightning: one bolt forking to three foes.
  ChainLightning: `${glow(0, 0, 44)}
${bolt("-44,-40 -18,-14 -26,-8 2,4", P, "#ffffff", 3)}
${bolt("2,4 20,-8 16,-16 40,-26", P, "#f0e6ff", 2.2)}${bolt("2,4 18,18 12,24 34,40", P, "#f0e6ff", 2)}${bolt("2,4 -8,22 -2,28 -24,42", P, "#f0e6ff", 1.6)}
<g fill="${P}"><circle cx="40" cy="-26" r="5"/><circle cx="34" cy="40" r="4.5"/><circle cx="-24" cy="42" r="4"/></g><circle cx="2" cy="4" r="4.5" fill="#ffffff"/>`,

  // Mind Leech: a siphon of warp energy drawn out of a victim's skull.
  ChainLightning_EX: `${glow(-20, 22, 34)}
${use("skull", "translate(22 -18) scale(0.6)")}
<circle cx="22" cy="-20" r="20" fill="${P}" opacity="0.2" filter="url(#soft)"/>
${glowPath("M14,-6 Q22,8 6,12 Q-10,16 -6,24 Q-2,32 -18,30", P, "#f0e6ff", 2.2)}
<circle cx="-22" cy="28" r="11" fill="${P}" opacity="0.6" filter="url(#soft)"/><circle cx="-22" cy="28" r="7" fill="#e8d8ff"/><circle cx="-22" cy="28" r="3.5" fill="#ffffff"/>
${embers([[8, 2], [-4, 18], [16, -2, 0.9]], "#e8d8ff")}`,

  // Discharge: a snap that arcs into every Charged enemy at once.
  Discharge: `${glow(0, 0, 46)}
${bolt("0,0 -12,-14 -8,-20 -22,-36", P, "#ffffff", 1.8)}${bolt("0,0 16,-6 18,-14 36,-18", P, "#ffffff", 1.8)}${bolt("0,0 6,16 0,22 10,38", P, "#ffffff", 1.8)}${bolt("0,0 -16,8 -22,4 -38,14", P, "#ffffff", 1.6)}
<g fill="#e8d8ff" stroke="${P}" stroke-width="1.5"><path d="M-22,-42 L-18,-36 L-22,-30 L-26,-36 Z"/><path d="M40,-18 L36,-14 L32,-18 L36,-22 Z" transform="translate(4 0)"/><path d="M10,44 L14,38 L10,32 L6,38 Z"/><path d="M-42,14 L-38,10 L-34,14 L-38,18 Z" transform="translate(-4 0)"/></g>
<circle r="11" fill="${P}" opacity="0.6" filter="url(#soft)"/><circle r="6" fill="#ffffff"/>`,

  // Psychic Barrier: a telekinetic bubble that soaks damage.
  PsychicBarrier: `${glow(0, 0, 46)}
<circle r="34" fill="${P}" fill-opacity="0.18" stroke="${P}" stroke-width="3"/>
<circle r="28" fill="none" stroke="#e8d8ff" stroke-width="1" opacity="0.6"/>
<path d="M-24,-16 A30,30 0 0 1 4,-30" fill="none" stroke="#ffffff" stroke-width="2.5" opacity="0.8" stroke-linecap="round"/>
<g fill="none" stroke="#d9c2ff" stroke-width="0.8" opacity="0.45"><polygon points="0,-12 10,-6 10,6 0,12 -10,6 -10,-6"/><polygon points="0,-24 10,-18 10,-6 0,-12 -10,-6 -10,-18"/><polygon points="20,-6 20,6 10,12 10,0"/><polygon points="-20,-6 -20,6 -10,12 -10,0"/></g>
${star(-34, 0, 7, 2, 4, "#ffffff")}${star(26, 22, 5, 1.6, 4, "#ffffff")}`,

  // Psychic Scream: a wail of fear rolling out from a screaming skull.
  PsychicBarrier_EX: `${glow(0, 0, 46)}
<g fill="none" stroke="${P}" stroke-linecap="round"><path d="M-30,-26 A40,40 0 0 0 -30,26" stroke-width="3" opacity="0.8"/><path d="M-40,-34 A52,52 0 0 0 -40,34" stroke-width="2" opacity="0.5"/><path d="M30,-26 A40,40 0 0 1 30,26" stroke-width="3" opacity="0.8"/><path d="M40,-34 A52,52 0 0 1 40,34" stroke-width="2" opacity="0.5"/></g>
${use("skull", "translate(0 -2) scale(0.8)")}
<ellipse cx="0" cy="13" rx="8" ry="10" fill="#120e0a"/>
<circle cx="-8" cy="-4" r="2.4" fill="${P}"/><circle cx="8" cy="-4" r="2.4" fill="${P}"/>`,

  // Warp Step: through the warp, leaving a pool of warp-fire behind.
  WarpStep: `${glow(18, -14, 36)}
<ellipse cx="-22" cy="28" rx="18" ry="6" fill="${P}" opacity="0.5"/>
<path d="M-26,18 Q-30,6 -22,-2 Q-24,8 -16,12 Q-12,4 -14,-6 Q-4,6 -12,20 Z" fill="#d9c2ff" opacity="0.85"/>
<path d="M-12,8 Q0,-34 20,-16" fill="none" stroke="${P}" stroke-width="2" stroke-dasharray="4 4"/>
<ellipse cx="20" cy="-14" rx="12" ry="18" fill="#0a0612" stroke="${P}" stroke-width="3"/>
<ellipse cx="20" cy="-14" rx="7" ry="12" fill="none" stroke="#e8d8ff" stroke-width="1.2" opacity="0.8"/>
<path d="M20,-26 Q28,-14 20,-2 Q14,-14 20,-26" fill="${P}" opacity="0.6"/>`,

  // Cripple: a slow, heavy bolt that stuns.
  Cripple: `${glow(8, -4, 40)}
<path d="M-46,30 Q-20,14 -4,4" fill="none" stroke="${P}" stroke-width="18" opacity="0.2" filter="url(#soft)" stroke-linecap="round"/>
<circle cx="8" cy="-4" r="17" fill="#2a1450" stroke="${P}" stroke-width="3"/>
<path d="M8,-16 A12,12 0 1 1 -2,2" fill="none" stroke="#d9c2ff" stroke-width="2"/>
<path d="M8,-10 A6,6 0 1 1 2,-2" fill="none" stroke="#ffffff" stroke-width="1.5"/>
${stunStars(8, -30, 16)}`,

  // Gravity Well: a singularity dragging everything into its centre.
  Cripple_EX: `${glow(0, 0, 48)}
<g fill="none" stroke-linecap="round"><path d="M-38,-10 Q-30,-34 0,-32 Q28,-30 30,-8" stroke="${P}" stroke-width="3" opacity="0.7"/><path d="M38,10 Q30,34 0,32 Q-28,30 -30,8" stroke="${P}" stroke-width="3" opacity="0.7"/><path d="M-24,-6 Q-18,-22 0,-20 Q18,-18 20,-4" stroke="#e8d8ff" stroke-width="1.5" opacity="0.7"/><path d="M24,6 Q18,22 0,20 Q-18,18 -20,4" stroke="#e8d8ff" stroke-width="1.5" opacity="0.7"/></g>
<circle r="12" fill="${P}" opacity="0.7" filter="url(#soft)"/><circle r="9" fill="#05030a" stroke="#e8d8ff" stroke-width="1.5"/>
<g fill="#8a7a6a"><rect x="-40" y="20" width="4" height="4" transform="rotate(20 -38 22)"/><rect x="36" y="-26" width="3.5" height="3.5" transform="rotate(40 38 -24)"/><rect x="30" y="30" width="3" height="3"/></g>
${chevrons(-46, 0, 2, 7, 4, "#d9c2ff", 0.6)}${chevrons(46, 0, 2, -7, 4, "#d9c2ff", 0.6)}`,

  // Psychic Blast: a channelled beam of white lightning.
  PsychicBlast: `${glow(0, 0, 48)}
<line x1="-40" y1="40" x2="50" y2="-50" stroke="${P}" stroke-width="26" opacity="0.3" filter="url(#blur4)"/>
<line x1="-40" y1="40" x2="50" y2="-50" stroke="${P}" stroke-width="13" opacity="0.8"/>
<line x1="-40" y1="40" x2="50" y2="-50" stroke="#ffffff" stroke-width="5"/>
${bolt("-24,14 -14,4 -20,0 -6,-12 -12,-18 4,-28", "#e8d8ff", "#ffffff", 1)}${bolt("-10,30 0,20 6,24 18,10 14,4 28,-6", "#e8d8ff", "#ffffff", 1)}
<circle cx="-38" cy="38" r="12" fill="#ffffff" opacity="0.8" filter="url(#soft)"/>
${embers([[30, 10], [-20, -30], [40, -20, 0.9]], "#e8d8ff")}`,
});
