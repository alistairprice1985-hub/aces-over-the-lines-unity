# Behaviour, Psychology, Physiology, and Tactical Decision-Making of WW1 Fighter Pilots, 1916–1918

*A Research Note for AI Opponent Modelling*

> **Editorial note:** this is a markdown rendering of the .docx research note (master copy held separately). The text has been encoding-cleaned (smart quotes, en/em-dashes, German umlauts, mathematical operators) but otherwise reflects the source verbatim. Two tables (Risk Tolerance profiles, §5.2; Skill Tiers, §5.3) and the Completion Table at the end were mashed without column separators in the .docx extraction and have been reconstructed as proper markdown tables from context.

## TL;DR

- The late-war pilot was not a stoic knight of the air but a young man with very short median life expectancy, varying levels of training, profound fear of fire, hypoxia-degraded judgement above ~15,000 ft, and a small, hard-won repertoire of "Dicta Boelcke"–derived tactics. AI should be modelled by skill tier (rookie / regular / veteran / ace), morale state, and physiological state — not as uniformly aggressive.
- AI tactics should hinge on energy and position (altitude, sun, surprise, formation) rather than turn-fighting in the abstract: veterans like McCudden and Mannock stalked, "bounced" from height, and broke off the moment the geometry turned against them; Voss, Ball, and the green pilot are the exceptions that prove the rule.
- AI damage and wound states should be graduated and probabilistic — the canonical historical cases (Voss continuing to fight with a chest wound; Mannock's incapacitation signalled by ceasing to kick rudder; Ball's vertigo in cloud; Udet's parachute hang-up on his rudder; the great fear of "the flamerinoes") give a programmable taxonomy of plausible reactions.

## Key Findings

### 1. The Dicta Boelcke (1916)

The single most important text for AI behaviour. The eight rules, as drafted by Oswald Boelcke at the request of Oberst Hermann von der Lieth-Thomsen and distributed throughout the Fliegertruppe/Luftstreitkräfte in mid-1916, are:

1. Try to secure advantages before attacking. If possible, keep the sun behind you.
2. Always carry through an attack when you have started it.
3. Fire only at close range, and only when your opponent is properly in your sights.
4. Always keep your eyes on your opponent, and never let yourself be deceived by ruses.
5. In any form of attack it is essential to assail your opponent from behind.
6. If your opponent dives on you, do not try to avoid his onslaught, but fly to meet it.
7. When over the enemy's lines, never forget your own line of retreat.
8. For the Staffel: Attack on principle in groups of four or six. When the fight breaks up into a series of single combats, take care that several do not go for one opponent.

These were applied operationally by every successful late-war German pilot and quietly adopted by the British (Mannock's "Rules of Combat" are a near-paraphrase, with added clauses on physical fitness, gun maintenance, and target practice). They are the AI's default rule set. Boelcke himself died on 28 October 1916 in a mid-air collision with Erwin Böhme, ironically violating Rule 8.

### 2. Named pilots — personal styles are real and distinct

A simulation should expose them via parameter sets:

- **Boelcke:** tactical genius, team-fighter, drilled wingman pairs spaced ~60 m abreast — fired only "when I can see the goggle strap on my opponent's crash helmet." His biography *Knight of Germany* (Johannes Werner, English trans. Claud W. Sykes, 1933) is the canonical source.
- **Manfred von Richthofen:** clinical, positional, conservative; preferred the certain shot to the spectacular manoeuvre; methodically stalked; deteriorated after his 6 July 1917 head wound. In *Der rote Kampfflieger / The Red Battle Flyer:* "I am in wretched spirits after every aerial combat. But that is surely one of the consequences of my head wound. When I put my foot on the ground again at the airfield, I go directly to my four walls, I do not want to see anyone or hear anything." Floyd Gibbons's *The Red Knight of Germany* (1927) is the popular English biography; the Allmers (*The Lancet*, 1999) and Hyatt/Orme (2004) papers argue the brain injury contributed to his uncharacteristic low pursuit of "Wop" May on 21 April 1918.
- **Werner Voss:** virtuoso individualist; relied on rotary-engine flick turns, flat skids, and sideways-firing snapshots. Capt. Geoffrey Bowman wrote: "he kicked on full rudder, without bank, pulled his nose up slightly, gave me a burst while he was skidding sideways and then kicked on opposite rudder before the results of this amazing stunt appeared to have any effect on the controllability of his machine."
- **Albert Ball:** lone wolf, point-blank closer (50 yards / 45 m), under-and-up attacks with the overwing Lewis on Nieuports; aggressive, exhausted, increasingly fatalistic by spring 1917. In a letter to his father: "I like this job, but nerves do not last long, and you soon want a rest."
- **Mick Mannock:** ruthless careful patrol leader; "the days of the lone fighter was past"; protected his rookies, set them up for kills, was phobic about fire and carried a revolver: "The other fellows all laugh at me for carrying a revolver. They think I'm going to shoot down a machine with it, but they're wrong. The reason I bought it was to finish myself as soon as I see the first signs of flames. They'll never burn me." Norman Macmillan's *Into the Blue* and Ira Jones's *King of Air Fighters* (1934) are the period sources.
- **James McCudden:** scientific, technical, the "stalker"; tuned his SE5a to add ~4,000 ft of ceiling so he could ambush high-altitude two-seaters from above and behind. He explicitly chose his fights ("when the Germans had the advantage, he broke off and headed for home"); 21 of his 57 victims fell in Allied lines — the highest such ratio of the war. *Flying Fury — Five Years in the Royal Flying Corps* (1918), written by McCudden himself days before his death.
- **Ernst Udet:** aggressive, theatrical, energy-efficient; modelled himself on Guynemer — "coming in high out of the sun to pick off the rear aircraft in a squadron before the others knew what was happening." Memoir: *Mein Fliegerleben* (1935; English ed. *Ace of the Iron Cross*).
- **Billy Bishop:** aggressive Canadian, lone solo raids, controversial but prolific. Memoir: *Winged Warfare* (1918).
- **Eddie Rickenbacker:** late-war American, methodical, drew heavily on French and British practice (Lufbery's tutoring). Memoir: *Fighting the Flying Circus* (1919).
- **Duncan Grinnell-Milne:** representative "good but not great" RFC pilot; his memoir *Wind in the Wires* (1933) is one of the most candid period accounts of fear, capture, escape, and squadron life.

### 3. The "Hun in the sun" doctrine and "Fokker Scourge" (1915–16)

These describe respectively the operational doctrine (gain altitude, position upsun of the target, dive on the unwary) and the early-war morale collapse of the RFC under the Fokker Eindecker. Both are behavioural lenses for the AI: a sun-side ambush should be the veteran's preferred entry, and the "moral effect" of a known feared type (D.VII in 1918) should bias rookie AI toward disengagement or panic.

### 4. Pilot life expectancy

The "11 days for a new subaltern" figure is from Peter Hart's *Bloody April: Slaughter in the Skies Over Arras, 1917.* Per the RFC War Diary as compiled in the Wikipedia entry for *Bloody April:* "245 aircraft down, 211 aircrew killed or missing, and 108 as prisoners of war. The German Air Services recorded the loss of 66 aircraft during the same period. Under Richthofen's leadership, Jasta 11 scored 89 victories during April, over a third of the British losses." Peter Hart (in *Key Military Magazine, The Myth of 'Bloody April'*) further records "92 hours of flight time per death" for that month. The figure properly applies to the April 1917 catastrophe, not to the whole war, but is widely (and reasonably) used as the canonical "rookie risk" datum.

### 5. Hypoxia, cold, and oil-spray — the unspoken third combatant

Above ~10,000 ft pilot performance degrades; above ~15,000 ft it becomes seriously impaired without oxygen, which RFC/RAF and US pilots did not have (only some German two-seaters and special high-altitude reconnaissance machines). Sholto Douglas, then commanding 84 Sqn, quoted in Douglas H. Robinson's *The Dangerous Sky: A History of Aviation Medicine* (1971): "I found that pushing the Lewis gun back into the fixed position while flying in the open cockpit of the SE5 at high altitude called for an effort that was almost superhuman. We had no supply of oxygen in those days, and I found that my strength at height fell off considerably." Ambient temperature at altitude was around −20 °C; pilots greased their faces with whale fat against frostbite. Castor oil from the rotary engine's total-loss lubrication system sprayed onto goggles and caused diarrhoea. McCudden in *Flying Fury:* "such a thing as having dirty goggles makes all the difference between getting or not getting a Hun."

### 6. Fire was the great fear

Allied pilots had no parachutes until September 1918 (and largely not even then); German pilots received the Heinecke seat-pack from mid-1918. Per Jon Guttman's HistoryNet article *Heinecke Parachute: A Leap of Faith for WWI German Airmen:* "a full third of the first 70 airmen to bail out died, in some instances because the static line tangled, the chute caught on the fuselage or the harness broke free." The first successful combat bailout was on 1 April 1918, when Vizefeldwebel Weimar jumped clear of his stricken Albatros D.Va. Mannock carried a revolver to shoot himself before flames reached him. Lufbery told Rickenbacker he would try to side-slip to keep flames off the cockpit and never jump; he was killed on 19 May 1918 either jumping or being thrown from his burning Nieuport 28 (Royal D. Frey's 1962 reinvestigation suggests he was thrown when the aircraft flipped after he had unbuckled to clear a Lewis-gun jam). Max Ritter von Müller (Jasta Boelcke, 9 January 1918) jumped without a parachute rather than burn. Mick Mannock's own death on 26 July 1918 is the canonical engine-fire-then-incapacitation sequence (see eyewitness account under Wound States below).

### 7. The Sopwith Camel and Fokker D.VII are mechanically opposed instruments

The Camel was a tail-heavy, gyroscopically violent, sensitive rotary-engined dogfighter that killed almost as many of its own trainees as it did Germans. Per War History Online (*It Wiped Out Large Numbers of Its Own Pilots*), citing official RFC casualty records: "The official figures are stark — 413 Camel pilots are noted as having died in combat during World War One while 385 were killed in non-combat accidents. Most of the accidents affected new pilots learning to fly the Camel." The article notes this still undercounts the true toll because "inexperienced pilots who died when they simply lost control of their unstable aircraft during the chaos of combat" are logged as combat, not accident, deaths. The Camel turned faster to the right (nose-down) than to the left (nose-up), gave the experienced pilot a uniquely tight right turn, and was lethal to the novice. The Fokker D.VII was stable, forgiving, retained controllability at the stall (the famous "hung on its prop" — pilots quoted in primary German sources: "*Das Flugzeug hängt am Propeller!*" / "The plane is like hanging on its propeller!"), and could fire upward into the bellies of higher-flying machines. Per the Wikipedia *Fokker D.VII* article: "Rate of climb: 3.92 m/s (772 ft/min) [Mercedes]; with BMW IIIa engine — 9.52 metres per second (1,874 ft/min)." The suffix "F" stood for Max Friz, the BMW engine designer; BMW-engined aircraft entered service with Jasta 11 in late June 1918. The BMW IIIa was over-compressed: "using full throttle at altitudes below 2,000 m (6,600 ft) risked premature detonation in the cylinders and damage to the engine."

---

## Details

### 1. AI Behavioural State Machine

The simplest defensible AI architecture for a WW1 dogfight simulation is a hierarchical state machine in which each AI has:

- A **skill tier** (Rookie / Regular / Veteran / Ace) — fixed at instantiation, gates which states are available, and parameterises thresholds.
- A **morale / nerve state** (Fresh / Steady / Wind-Up / Broken) — drifts over a sortie and across a campaign; modulates aggression and disengagement thresholds.
- A **physiological state** (Healthy / Cold / Hypoxic / Wounded-light / Wounded-serious / Incapacitating) — degrades control inputs and reaction time.
- A **combat state** (Patrol / Search / Stalk / Bounce / Engage / Evade / Disengage / RTB / Wounded / Damaged / Out-of-Ammo / Burning / Spinning) — the active behavioural mode.

#### 1.1 PATROL

- Default: cruise altitude band 10,000–17,000 ft for fighters in 1918, formation by national doctrine (see §3 below).
- Scan cones: forward (60°), upper-rear (the canonical blind spot, hence "beware the Hun in the sun"), sides.
- Veterans use a head-on-a-swivel model with an additional bias toward the up-sun quadrant (probability of detecting an out-of-sun threat halved relative to other quadrants).
- Rookies have narrower scan cones and a longer reaction lag (e.g., 1.5–2.0 s) to detected contacts. Cecil Lewis in *Sagittarius Rising:* "The most self-confident aces began to wonder when their turn would come… faced by the empty chairs of men you had laughed and joked with at lunch."

#### 1.2 SEARCH (post-contact)

- On detecting an unknown contact: veterans climb toward the sun and assess (numbers, type, altitude) for many minutes before committing. McCudden tuned his SE5a's engine to gain altitude precisely to enable this kind of stalking — he is the prototypical "Search → Stalk" AI.
- Rookies have a much higher probability of moving directly to ENGAGE without an intermediate STALK; this is the documented green-pilot error pattern (Mannock's first weeks; Udet's first combat in December 1915, when he froze in a head-on and lost his goggles to French fire).

#### 1.3 STALK

- Used by Veterans/Aces. The AI shadows the target from a position upsun and above, matching speed, refusing to be drawn down. Duration can be many minutes. McCudden in *Flying Fury* described "studying the habits and psychology of enemy pilots and stalking them with patience and tenacity." Triggers transition to BOUNCE on: (a) target descends, (b) the AI achieves a textbook 6 o'clock high position, (c) target's own escorts have drifted away.

#### 1.4 BOUNCE

- The diving attack from up-sun, at high speed, with a single firing pass and zoom-climb away. The classic Boelcke / McCudden / Udet / Richthofen attack. Should be the AI's preferred engagement if energy and surprise are achievable.
- Fire opens at point-blank — Mannock's "within 100 yards" rule, Ball's 50 yards, Boelcke's "goggle strap." Beyond ~150 yards, AI fire accuracy should collapse to near zero (matches McCudden's repeated complaints about long-range shooting).

#### 1.5 ENGAGE (turn fight)

- Used when energy is lost or geometry forces a horizontal fight. The AI's choice of turn direction must respect aircraft characteristics:
    - Camel AI: biased to right turn (gyroscopic), nose-down.
    - D.VII AI: stable, can sustain low-speed handling close to stall — uses the "hang on the prop" trick to outclimb an opponent in a slow vertical scissors.
    - Albatros D.V / Pfalz D.III: avoid prolonged turning with Camels/SE5as; rely on dive-and-zoom.

#### 1.6 EVADE

Standard manoeuvres:

- **Split-S / dive-out:** half-roll then half-loop, trading altitude for distance and speed (Allied pilots' favoured exit from a losing high fight).
- **Defensive (Lufbery) circle** if a formation has been bounced — each aircraft covers the tail of the one ahead. Effective against horizontal attackers, "very vulnerable to attacks from fighters diving from above" (Wikipedia, citing the F.E.2 origin in the Fokker Scourge of 1916). Bombers and reconnaissance two-seaters use this; single-seaters use it only as a last resort.
- **"Immelmann turn"** — note historical reality: contemporary RNAS/RAF training manuals and modern aviation historians make clear that the WW1 manoeuvre Immelmann actually used was *not* the modern half-loop-half-roll. It was a zoom-climb after a diving attack, followed by a rudder-induced yaw (essentially a hammerhead/stall-turn) at the apex to point the aircraft back down for another pass. The French called this *Renversement.* The modern half-loop-half-roll was beyond the power-to-weight of most WW1 fighters and was generally suicidal in combat because the aircraft hung motionless at the top — "the Immelmann had already fallen somewhat into disfavour by 1917/1918, as it became obvious that the zooming aircraft presented an easy target as it hung nearly motionless at the top of the manoeuvre." AI should not perform the modern Immelmann; it should perform a wingover, chandelle, or stall-turn renversement, and only when energy permits.
- **Spin recovery:** a deliberate spin is a viable evasive technique only if the pilot trusts his ability to recover at low altitude — a Veteran trait. Rookies entering a spin often fail to recover (Camel trainees especially).

#### 1.7 DISENGAGE

- Boelcke Rule 7: never forget the line of retreat. AI tracks distance to friendly lines, fuel state, ammunition state, and wind direction. When over enemy territory and any two of {ammo low, fuel <40%, damage moderate, outnumbered ≥2:1, wounded} are true, the AI initiates DISENGAGE: dive west (for Allied) / east (for German) with rudder-jink at low altitude to spoil ground-fire aim.

#### 1.8 RTB (Return to Base)

- Straight-line cruise at economic altitude (~6,000–8,000 ft) toward home airfield; refuses further engagement unless ambushed; opens fire only in self-defence.

### 2. Wound and Damage States — A Behavioural Taxonomy

The primary-source record from 1916–1918 supports a graduated, probabilistic wound model.

#### 2.1 Pilot Wounds

**Leg / foot wound (rudder degraded):**

- Immediate effect: rudder authority reduced by 40–70%; ability to coordinate hard turns degraded; no immediate consciousness loss.
- Behavioural change: AI biases toward shallower turns; cannot perform precision low-altitude rudder-jinking; this is the documented mechanism by which a wounded pilot loses the ability to evade ground fire (cf. Mannock's incapacitation visible to Inglis as cessation of rudder kicks). Udet's own 28 September 1918 thigh wound from a DH.9's return fire ended his combat career without immediate incapacitation, supporting a "wounded but flyable" intermediate state.
- Blood-loss timer: 90–240 s before secondary symptoms (lightheadedness → reaction time +50%).
- Threshold to next state: if not put down within 5–10 minutes, transitions to Wounded-serious.

**Arm / hand wound (stick / throttle / gun):**

- Immediate effect: stick force / throttle modulation degraded; gun-jam clearance (which required physically hammering the cocking lever — Arthur Gould Lee in *Open Cockpit* describes the "2½ lb hammer, for rectifying gun-jams" carried in its leather socket in the Pup/Camel cockpit) becomes impossible. A hand wound effectively reduces the AI to whatever rounds were chambered.
- Behavioural change: switch to DISENGAGE immediately if any jam occurs.

**Torso (chest / abdomen) wound:**

- The Voss case (23 September 1917) is the canonical reference. Post-mortem evidence reported in Norman Franks's *Sharks Among Minnows* and Barry Diggens's *September Evening: The Life and Final Combat of the German World War One Ace Werner Voss:* one round through the right side of the chest, through the lungs (Hoidge's angle), after which Voss continued to fly and fire for tens of seconds before progressive incapacitation; two further wounds through the abdomen from rear to front (Rhys-Davids's angle); all approximately fatal within "less than a minute."
    - Single torso wound: 30–90 s of degrading capacity, then loss of fine motor control, glide/stall, crash.
    - The AI should *not* immediately go limp; it should fire one or two more bursts, then begin a passive shallow descent.
- Pain-shock: probability of involuntary stick-back (slight climb / stall) within 1–2 s of impact.

**Head wound — grazing / concussion:**

- Richthofen's 6 July 1917 wound (skull graze near Wervik against No. 20 Sqn FE2ds; "temporary partial blindness", recovered control in the spin and force-landed) is the prototype. From his combat narrative: "Suddenly something strikes me in the head." Behavioural effects: 5–15 s of impaired vision and disorientation; AI flies straight-and-level for that window before recovering. Long-term effects on Richthofen (per the Allmers/Hyatt hypothesis) — supports a persistent decrement to a pilot's tactical scores in a campaign mode after such a wound.

**Head wound — severe:**

- Immediate unconsciousness, slumping forward over the stick (which on rotary-engine fighters with weight forward translated into an immediate steep dive and uncontrolled descent). Hawker's death (23 November 1916) is the textbook example. Per Richthofen's *Der rote Kampfflieger / The Red Battle Flyer:* "When he had come down to about three hundred feet he tried to escape by flying in a zig-zag course… I followed him at an altitude of from two hundred and fifty feet to one hundred and fifty feet, firing all the time. The Englishman could not help falling. But the jamming of my gun nearly robbed me of my success. My opponent fell, shot through the head, one hundred and fifty feet behind our line."

#### 2.2 Aircraft Damage

**Engine failure / loss of thrust:** dead-stick glide. The veteran AI looks for a field within glide range (rough rule: ~10× altitude in feet = glide distance in feet for SE5/Camel); the rookie often turns back toward the airfield and stalls — which is exactly how McCudden himself died on 9 July 1918 (Cole, *McCudden VC*, 1967: "McCudden committed the basic error of trying to turn back to land rather than gliding straight on to a forced landing; and his aircraft stalled on the turn, and spun into the ground"). This is a beautiful, programmable Rookie/Veteran difference: Rookie attempts to turn back if engine fails after takeoff or in combat; Veteran chooses straight-ahead forced landing.

**Fire:** the most psychologically loaded state. Behavioural options to model, each weighted by skill, nationality, and morale:

- Side-slip dive (try to keep flames off the cockpit and possibly extinguish): the Lufbery method; common to British and American pilots.
- Jump (without parachute): documented for von Müller (Jasta Boelcke, 9 Jan 1918) and ambiguously for Lufbery (19 May 1918). Rare for British pilots in single-seaters; non-existent for German pilots after mid-1918 if they had a Heinecke chute.
- Suicide by service pistol: Mannock's stated intention; not many documented executions, but the fear drove the behaviour.
- Continue to fight to the end ("ride it down"): the stoic / paralysed option.
- Use the parachute (German pilots from mid-1918 only): success rate roughly two-thirds for the first 70 jumps. Udet's 29 June 1918 jump from Jasta 4 is the canonical example of a hairy but successful escape — harness caught on rudder; broke off rudder tip; chute opened at 76 m / 250 ft; sprained ankle. From his memoir *Mein Fliegerleben* (1935): the description of being thrown against the rudder by the air pressure and breaking off the rudder edge is widely reproduced.

**Wing or major structural damage:** AI recognises catastrophic damage by an attitude-rate sensor proxy (e.g., a sudden uncommanded roll rate >120 °/s). On detection: AI cuts throttle and attempts to set down anywhere — no further combat decisions. If damage is to a flying surface but the aircraft is still controllable, the AI shifts to RTB with shallow turns only. (Note: Albatros D.III/D.V wing-failure in dives is documented as a specific known weakness; Mannock himself survived an in-flight lower-right-wing collapse on his Nieuport on 19 April 1917.)

**Control-surface loss:**

- Elevator — AI uses trim/throttle to manage pitch; aggressive manoeuvre disabled; RTB.
- Rudder — AI cannot side-slip or yaw-to-aim; loses the Voss flat-skid trick; loses ground-fire jink (Mannock case).
- Aileron — AI uses rudder for shallow turns; cannot bank steeply; disengage.

**Out of ammunition:** classic egress problem. McCudden's combat report on 30 January 1918 (from the London Gazette VC citation): "he only returned home when the enemy scouts had been driven far east; his Lewis gun ammunition was all finished and the belt of his Vickers gun had broken." This shows the veteran continuing the manoeuvre fight even with no shooting capacity, to push the enemy off comrades. Behavioural rules:

- If accompanied: AI continues to feint and bluff (formate on friendly, scissor with enemy to spoil his aim) until friendlies disengage; only then runs.
- If alone: immediate dive for the lines, jinking.

#### 2.3 Hypoxia and Cold (Physiological State)

- Below ~10,000 ft: no effect.
- 10,000–14,000 ft: mild — reaction time +10%, scan rate slightly reduced.
- 14,000–17,000 ft: moderate — reaction time +30%, judgement degraded (random chance of misidentifying friendly as enemy or missing a contact entirely), fine-motor degraded (gun jams not cleared as quickly).
- 17,000–19,000 ft: severe — pilot cannot reliably operate gun mechanisms or perform sustained hard manoeuvres (cf. Sholto Douglas's "almost superhuman" effort to rack the Lewis); risk of greying-out on hard pulls.
- Above ~19,000 ft (only some D.VII(F)s and tuned SE5as like McCudden's): pilot may lose consciousness within minutes; risk increases with sortie duration and cold.
- Cold (always present above ~6,000 ft): degrades stick precision; goggles fog or freeze with oil; AI accuracy at high altitude reduced even before hypoxia kicks in.

### 3. Tactical Doctrine 1916–1918: From Lone Wolves to Formations

#### 3.1 The Evolution

- **1915 – early 1916 (Fokker Eindecker Scourge):** single-seat fighters as protective escort for two-seaters; aggressive individualists (Boelcke, Immelmann); Allied response is the defensive circle (the proto-Lufbery, with F.E.2bs).
- **Mid–late 1916 (Boelcke / Jasta 2):** Boelcke codifies the Dicta, founds Jasta 2 (10 August 1916), drills wingman pairs (60 m abreast) and squadron formations. Jasta tactics become the standard.
- **Bloody April 1917:** Albatros D.III + organised Jastas crush a numerically superior but technically and doctrinally inferior RFC. 245 aircraft lost, 211 aircrew killed or missing plus 108 POW (RFC War Diary). Jasta 11 alone scored 89 victories. The "11-day life expectancy" datum is from this month.
- **Mid 1917 onwards:** SE5a, Camel, SPAD XIII enter service. RFC patrols of 5–7 aircraft become standard. Tactics professionalise.
- **1918 (Fokker D.VII era):** both sides fight in organised formations; lone wolves are anomalies (Voss is the last great example, and he dies because of it).

#### 3.2 Formation Structures (AI Should Model These Faithfully)

- **German Kette:** three aircraft, leader and two wingmen.
- **Jasta:** a fighter squadron, paper strength ~14 aircraft, typical fighting strength ~8–12. Operated in two or three Ketten or in pairs.
- **Jagdgeschwader** (e.g., JG 1 — Richthofen's "Flying Circus"): four Jastas combined (Jastas 4, 6, 10, 11), highly mobile, deployed to local airspace where superiority was needed.
- **RFC/RAF patrol:** typically 5–7 aircraft (a Flight) under a Flight Commander; a Squadron of three flights rarely operated as a single formation but flights from the same squadron could rendezvous.
- **French Escadrille:** typically ~10 aircraft, similar pair-based tactics derived from Guynemer's influence.

AI in a formation should:

- Maintain assigned position (line abreast, vic, echelon) until contact.
- On contact, leader signals (wing-rock, Very pistol — no radio) and the formation splits into pairs.
- Each pair maintains visual contact (the rear man's primary job is the leader's tail — Mannock and Boelcke both emphasised this).
- After a pass, attempt to reform on the leader.

#### 3.3 Specific Manoeuvres — Historical Accuracy Notes

- **The dive from out of the sun:** foundational. Veterans use it; rookies miss the sun-side threat.
- **The climbing turn / chandelle:** trade speed for altitude with a heading change. The D.VII does this well at high altitude; the Camel does this well but with the dangerous left-turn nose-up tendency.
- **The Immelmann (as actually flown 1915–17):** a zoom-climb followed by a rudder-induced stall-turn / yaw at the apex, pointing the nose back down for another diving pass — *not* the modern half-loop-half-roll. Closer to a hammerhead.
- **Split-S:** half-roll + half-loop downward — used to disengage by trading altitude for speed and reversing course.
- **Lufbery circle / defensive circle:** see §1.6 above. "Lufbery did not invent the tactic; how it acquired this name is not known, although it may be from his popularization of it among the incoming U.S. pilots he trained. In non-American sources it is in fact usually referred to simply as a 'defensive circle'." (Wikipedia.)
- **The Voss flat skid / hammerhead snapshot:** a Fokker Dr.I (and to a lesser extent Camel) trick — full rudder without bank, brief upward yaw, snapshot, opposite rudder. Should be a Veteran/Ace-only manoeuvre; rare and visually distinctive.
- **Chandelle** (climbing turn through 180°): used to regain altitude after a diving attack and to set up another pass.

#### 3.4 The "Hun in the Sun" Doctrine

Operationally, the side with the height advantage chose the time of engagement. Late-war German Jastas typically refused fights they could not start from above; this drove the RFC's expensive offensive policy under Trenchard. Both sides used cloud cover to stalk: enter cloud, change course, exit elsewhere to surprise the enemy.

#### 3.5 Disengagement Criteria — When the AI Breaks Off

- Fuel below ~35–40% (with margin for crosswind back to base).
- Ammunition: out of one gun, breaking off when the second is also low.
- Outnumbered ≥ 2:1 without altitude advantage: Veterans break off; Aces sometimes fight on (Voss).
- Wounded to any degree: break off immediately.
- Damage affecting flight controls or engine: break off.
- Geometry lost (cannot regain a firing position within ~3 manoeuvres): break off and re-stalk, or RTB.

#### 3.6 Energy Management (Pre-Modern Vocabulary)

WW1 pilots did not use the term "energy" but understood the concept as height + speed = the ability to manoeuvre. McCudden, Mannock, and Boelcke wrote explicitly about the value of altitude and of avoiding fights at low speed. AI should treat altitude as a savings account: spend it on a diving attack, refuse engagement when bankrupt.

### 4. Aircraft-Specific Notes

#### 4.1 Sopwith Camel (F.1)

- **Performance:** ~110–115 mph, ceiling ~19,000 ft, twin synchronised Vickers .303 (the first British single-seater so armed).
- **Engine:** 130 hp Clerget 9B rotary (or Bentley BR.1, or 110 hp Le Rhône). Total-loss castor-oil lubrication.
- **Handling:** extremely tail-heavy at level flight (Wikipedia: pilot maintained "constant forward pressure on the control stick"). Gyroscopic torque from the rotating cylinder mass makes right turns faster and nose-down; left turns slower and nose-up. Arthur Harris, future Marshal of the RAF, quoted in Mark C. Wilkins's *British Fighter Aircraft in World War I:* "If you wanted to go into a left turn you put on full right rudder, and if you let go of the stick it looped!"
- **Trainee danger:** 413 combat deaths vs 385 non-combat deaths (War History Online, official RFC records). AI should model the Camel as a Veteran's tool: rookie AI in Camels should have a meaningful probability of stalling/spinning in hard manoeuvres.
- **Tactic:** tight right-hand turn fight at medium altitude; weak above 15,000 ft.
- **Visibility:** cockpit relatively poor due to forward-mounted top wing and centre-section.

#### 4.2 Fokker D.VII / D.VII(F)

- **Performance:** ~117 mph (Mercedes D.IIIaü) or ~124 mph (BMW IIIa), ceiling ~19,700 ft (higher in the F variant), twin Spandau (LMG 08/15) 7.92 mm.
- **Handling:** stable, forgiving, retains aileron and elevator authority near the stall — the "hung on the prop" phenomenon. Smithsonian National Air and Space Museum: "the soon-to-be-famous ability of the Fokker D.VII to seemingly 'hang on its propeller,' and fire into the unprotected underside of Allied two-seater reconnaissance aircraft."
- **BMW IIIa:** over-compressed; full throttle below ~2,000 m / 6,600 ft risked detonation. D.VII(F) climb 1,874 ft/min vs Mercedes D.VII 772 ft/min (Wikipedia, *Fokker D.VII*).
- **Tactic:** zoom-climb to engage from above, exploit high-altitude superiority; safe even for newer pilots ("easier to train new pilots, and to keep less experienced pilots out of trouble"). The Armistice agreement uniquely required all D.VIIs to be surrendered — a measure of how feared it was.

#### 4.3 Context Aircraft (for later opponents)

- **Albatros D.III / D.V:** 1917 mainstay; structurally weak lower wing (Albatros wing failures in dives are documented); twin Spandau; ~110 mph; the dominant Bloody April scout. Alex Imrie's *Pictorial History of the German Army Air Service 1914–18* is the standard work.
- **Pfalz D.III:** tougher, less agile alternative to the Albatros; favoured by some Jastas.
- **Fokker Dr.I:** 1917 triplane; superb climb and turn but slow and structurally suspect (early production wing failures); Voss's mount.
- **SE5a:** stable, fast, good gun platform; Mannock's and McCudden's mount.
- **SPAD XIII:** fast, dive-capable, less manoeuvrable; American and French standard; Rickenbacker's mount.

### 5. Psychological Dimensions

#### 5.1 Aggression Cycle and the "Buck"

A documented pattern across memoirs (Mannock, Ball, Bishop *Winged Warfare*, Udet, Grinnell-Milne *Wind in the Wires*):

1. **Pre-combat green pilot:** nervous, slow, hesitant. Mannock's early weeks were so timid that fellow officers called him "windy" — having "the wind up."
2. **First kill:** relief, often nausea. Mannock visited his first victim's wreck, saw the dog dead in the observer's seat, and wrote in his diary "I felt exactly like a murderer." Udet, in *Mein Fliegerleben*, recounts freezing in his first head-on and being hit in the face by French fire.
3. **Spurt of aggression:** streak of kills; risk-taking peaks; target fixation kills many here. Ball's solo aggression peaked in autumn 1916; in Jill Bush's account for Historic UK: "His commander knew of the stress that had so sapped his energies." Ball himself wrote home: "I like this job, but nerves do not last long, and you soon want a rest."
4. **Plateau / professionalisation:** the veteran emerges (Mannock, McCudden). Combat is calculated. Stalking dominates over impulsive engagement.
5. **Combat fatigue / "the buck":** nerves accumulate. Mannock broke down weeping when on leave in June 1918; recorded in his diary phobic dreams of being burned alive ("That's the way they're going to get me in the end — flames and finish"). Voss, returning from leave on 23 September 1917: fellow pilot Leutnant Alois Heldmann said he had "the nervous instability of a cat. I think it would be fair to say he was flying on his nerves." He died that evening.
6. **Premonitions and fatalism:** Mannock confided to a friend after McCudden's death that "three was an unlucky number" (his third combat tour). The most superstitious behaviours (refusing to fly on Friday the 13th, the "13 at table" rule) became standard. From Look and Learn's account of "Bloody April": "They were a superstitious lot. Some would not drink until a drop had been spilled on the floor; others touched wood, and few would sit down at table if 13 people were present."

#### 5.2 Risk Tolerance — Three Documented Profiles

| Profile | Risk-taking pattern | Behavioural correlate |
|---|---|---|
| **Green / Rookie (ignorance)** | High risk-taking due to inexperience: misjudges range, dives into bad geometry, fails to check upsun, attacks single targets without checking for escorts. | Rookie AI takes risks the AI itself does not "understand" are risks. Often dies in first sortie. |
| **Veteran (calculated)** | Low absolute number of risky decisions; high quality of judgement on which risks to take. Refuses bad fights. McCudden's pattern. | Veteran AI uses many conditions before committing: altitude, sun, escorts, geometry, fuel/ammo. |
| **Broken / Fatalistic** | Two opposite poles: (a) excessive caution — aborting patrols, faking engine trouble, hanging back ("I felt out of luck today"); or (b) reckless death-seeking — taking impossible shots, ignoring own rules (Mannock circling the burning DFW twice at low altitude in his last fight, against his own teaching). | Broken AI either refuses to engage or commits to suicidal pursuits; should appear as a campaign-mode state, not a default. |

#### 5.3 Skill Tiers — Recommended Default Parameters

| Skill | Hours | Scan radius | Reaction lag | Fire accuracy | Manoeuvre repertoire | Disengage threshold | Spin recovery |
|---|---|---|---|---|---|---|---|
| **Rookie** | <50 | 0.5× | 2.0 s | 0.3× | Basic turns, dives | Engages everything; doesn't disengage | Low (often fatal) |
| **Regular** | 50–150 | 0.8× | 1.2 s | 0.7× | + chandelle, split-S | Disengages when wounded or outnumbered 3:1 | Moderate |
| **Veteran** | 150–300 | 1.0× | 0.8 s | 1.0× | + Immelmann (WW1), stall turn, deflection shots | Disengages when conditions unfavourable | High |
| **Ace** | 300+ | 1.2× | 0.6 s | 1.2× | + flat-skid snapshot, predictive deflection, sun-stalk | Chooses engagements; refuses bad ones | Reliable |

Plus per-personality biases (Voss = +Aggression, +Risk; McCudden = +Patience, +Altitude; Mannock = +Formation discipline, +Wingman protection; Richthofen = +Conservatism, +Positional; Ball = +Solo, +Closing range; Bishop = +Solo, +Aggression).

The Bloody April training context for the lowest tier is sobering: per Look and Learn, "Incredibly, in the middle of 'Bloody April', they were being sent to the Western Front with only 17½ hours instruction behind them, which made it even easier for crack German pilots to shoot them down. By September, however, the training time had been raised to 48½ hours."

#### 5.4 The "Twitchiness" of the Veteran

The veteran's scan is sensitive to small cues: a glint of varnish or perspex, a shadow out of place, a wisp of smoke from an engine. Veteran/Ace AI should detect contacts at proportionally greater range and respond to subtler cues (e.g., an aircraft "just" out of position in a formation, suggesting an enemy infiltrator).

#### 5.5 The "I felt out of luck today" syndrome

Documented in memoirs (Cecil Lewis, Mannock, Grinnell-Milne): pilots occasionally refused to fly, faked engine trouble, or aborted patrols because of a presentiment. Campaign mode can model this as a low-probability random "refuse mission" or "abort patrol" event, scaled by accumulated stress.

#### 5.6 Moral Effect of Aircraft Type

- 1916: Allied pilots flinched from the Fokker Eindecker monoplane.
- April 1917: green RFC pilots were openly frightened of Albatros D.IIIs.
- 1918: the appearance of the D.VII reportedly caused Allied pilots, per the Museum of Flight, "to dread the appearance of the 'straight wings' with their 'coffin noses'." AI behavioural modifier: in a sim, a rookie player encountering D.VIIs should face AI that exploits its expected morale advantage (more aggressive openings, earlier engagement).

### 6. Communication, Equipment, and Cockpit Realities

- **No radios.** Communication by:
    - Wing-rocking (warning of enemy, "look here").
    - Very pistol flares (mission directives, recall).
    - Hand signals between formation members in close proximity.
- **Gun jams:** the dominant non-combat failure. Lee in *Open Cockpit* lists the cockpit contents he checked before every patrol: "the 2½ lb hammer, for rectifying gun-jams, was in its leather socket, as well as the Colt automatic pistol, the fire extinguisher, the Very pistol and cartridges, plus the slab of chocolate (one got peckish on long patrols), the prisoner-of-war haversack (shaving gear, toothbrush, socks, shoes and so on) and the handkerchief…" The Vickers and Spandau both jammed frequently. McCudden in *Flying Fury:* he "struggled with his balky machine guns, almost more than with the 'Huns.'" AI should model gun jams probabilistically (~5–15% chance per long burst), with clearance time of 5–20 s if both hands free, and impossible if one hand is wounded.
- **Castor oil spray and dirty goggles:** McCudden — "such a thing as having dirty goggles makes all the difference between getting or not getting a Hun." Modelled as a slowly accumulating accuracy penalty over a long sortie.
- **Frostbite:** greased faces, layered clothing. Above 15,000 ft, extremities numb within minutes. AI cold timer slowly degrades fine motor control.

### 7. Canonical Engagements as Behavioural Test Cases

1. **Hawker vs Richthofen, 23 November 1916.** DH.2 vs Albatros D.II. ~30-minute single-combat spiral, descending downwind toward German lines. Hawker's flight of four DH.2s of No. 24 Sqn attacked five Albatrosses; two DH.2s turned back, Andrews was hit, Hawker was suddenly alone over German territory. Richthofen's account (*Der rote Kampfflieger / The Red Battle Flyer*, trans. T. Ellis Barker, 1918): "First we circled twenty times to the left, and then thirty times to the right. Each tried to get behind and above the other. Soon I discovered that I was not meeting a beginner… The circles which we made around one another were so narrow that their diameter was probably no more than 250 or 300 feet… When he had come down to about three hundred feet he tried to escape by flying in a zig-zag course… I followed him at an altitude of from two hundred and fifty feet to one hundred and fifty feet, firing all the time. The Englishman could not help falling. But the jamming of my gun nearly robbed me of my success. My opponent fell, shot through the head, one hundred and fifty feet behind our line." Richthofen elsewhere called Hawker "the English Immelmann." **Test case for:** pursuit-to-the-lines behaviour, jink-to-escape, single-bullet incapacitation, the value of two guns vs one in an attritional fight.

2. **Voss vs 'B' Flight 56 Squadron, 23 September 1917.** Voss in a Fokker F.I/Dr.I 103/17 against 7–8 SE5a aces for approximately ten minutes. McCudden in *Flying Fury* (verbatim): "I shall never forget my admiration for that German pilot, who single handed, fought seven of us for ten minutes. I saw him go into a fairly steep dive and so I continued to watch, and then saw the triplane hit the ground and disappear into a thousand fragments, for it seemed to me that it literally went into powder." Voss's flat-skid snapshot, refusal to disengage when given clear chances, eventual death from a chest wound but with several seconds of continued flying afterward. **Test case for:** the Ace-aggressive virtuoso, the chest-wound progression, the multi-aircraft melee.

3. **Ball's death, 7 May 1917.** SE5a entering thundercloud near Annœullin; emerging inverted with dead propeller, crashing without battle damage. German ground witnesses (cited in Bowyer's *Albert Ball VC* and Franks/Bailey/Guest's *Above the Lines*) reported the inverted descent. **Test case for:** spatial disorientation in IMC, especially for AI not equipped with cloud-handling logic.

4. **Mannock's death, 26 July 1918.** Engine hit by ground fire after low pass over a flamed two-seater (DFW C.V) near La Pierre au Beure / Pacault Wood. Verbatim 2/Lt Donald C. Inglis's combat report: "Falling in behind Mick again we made a couple of circles around the burning wreck and then made for home. I saw Mick start to kick his rudder and realised we were fairly low, then I saw a flame come out of the side of his machine; it grew bigger and bigger. Mick was no longer kicking his rudder; his nose dropped slightly, and he went into a slow right-hand turn round, about twice, and hit the ground in a burst of flame. I circled at about twenty feet but could not see him, and as things were getting hot, made for home and managed to reach our outposts with a punctured fuel tank. Poor Mick… the bloody bastards had shot my major down in flames." **Test case for:** engine-fire-then-pilot-incapacitation signalled by cessation of evasive rudder input.

5. **Udet's parachute jump, 29 June 1918.** Spinning D.VII; Heinecke pack; harness caught on rudder; broke off rudder tip; chute opened at ~250 ft / 76 m; sprained ankle on landing. From *Mein Fliegerleben* (1935): "On 29 June 1918, he jumped after a clash with a French Breguet. His harness caught on the rudder and he had to break off the rudder tip to escape. His parachute did not open until he was 250 ft (76 m) from the ground, causing him to sprain his ankle." **Test case for:** the German parachute escape, including realistic failure modes.

### 8. National / Service-Specific Behavioural Defaults

- **RFC/RAF (1916–1918):** aggressive offensive posture (Trenchard doctrine — "the offensive must be maintained at all times"). Patrols deep into German airspace. High loss tolerance. AI: tends to press attacks; reluctant to disengage even at unfavourable odds; flight commanders model after Mannock — protective of rookies. Patrol size 5–7.
- **Luftstreitkräfte (Jasta):** defensive-offensive doctrine — most fights over German lines. Conserves pilots ("such operations risked attrition that the Luftstreitkräfte could ill-afford" — Wikipedia on Jagdstaffel operations 1917–18). Refuses engagements without altitude advantage. Has parachutes from mid-1918 (modify fire-state behaviour accordingly). Jasta size 8–12 effective.
- **French Aéronautique Militaire:** Guynemer/Fonck individualist tradition; tight pairs; sun-tactics; deflection shooting (Fonck especially).
- **US Air Service (1918):** green; absorbs French and British doctrine through Lufbery and Issoudun training. Aggressive, often imprudent. Rickenbacker's memoir *Fighting the Flying Circus* emphasises learning the hard way.

## Recommendations

### Programming Roadmap (Staged)

1. **Stage 1 — Behavioural state machine and skill tiers.** Implement the eight states in §1 with the skill-tier parameter table in §5.3. This alone produces noticeably distinct AI tiers.
2. **Stage 2 — Aircraft-specific manoeuvre selection.** Camel AI right-turns; D.VII AI zoom-climbs and "hangs on the prop." This grounds the AI in the aircraft physics.
3. **Stage 3 — Wound and damage states (§2).** Implement the graduated probabilistic wound model with Voss-style "fight-for-30-seconds-then-degrade" curves for torso wounds, Mannock-style "stop kicking rudder" for incapacitation, Hawker-style "instant slump" for head shots.
4. **Stage 4 — Fire behaviour (§2.2).** Implement weighted fire-response options by nationality and skill: side-slip-dive (BR/US), jump-without-parachute (rare, mostly DE pre-mid-1918), parachute (DE post-mid-1918), service-pistol suicide (rare), ride-it-down (common).
5. **Stage 5 — Personality overlays.** Add per-ace personality parameter sets (Voss, Mannock, McCudden, Richthofen, Udet, Ball, Bishop) for named opponents in scripted missions.
6. **Stage 6 — Campaign / morale layer.** Track pilot stress over sorties; introduce "I feel out of luck today" abort behaviour; transition state Fresh → Steady → Wind-Up → Broken affecting all behavioural thresholds.

### Benchmarks for Tuning

- A Rookie AI should die within its first 2–3 sorties against a moderate-skill player ~70–80% of the time — this matches "Bloody April" historicity (Hart's "11 days" for new RFC subalterns).
- A Veteran AI should disengage 60–70% of fights it does not start from advantage. McCudden's record (21 of 57 victims fell in Allied lines; he repeatedly refused unfavourable engagements) is the benchmark.
- A Camel AI flown by a rookie should crash in ~30% of high-G manoeuvres at low altitude (broadly consistent with the 385 non-combat Camel deaths in the official RFC records).
- Heinecke parachute should fail in ~33% of uses for the first generation (matches "a full third of the first 70 airmen to bail out died" per Guttman/HistoryNet).
- Above 15,000 ft, Allied AI should perform measurably worse than D.VII(F) AI — driven by hypoxia and the BMW IIIa's altitude-tuned carburettor advantage (D.VII(F) climb 1,874 ft/min vs Mercedes D.VII 772 ft/min).

### Thresholds That Should Change Recommendations

- If players find Rookie AI too easy or Veteran AI too hard, adjust the scan-radius and reaction-lag parameters first (these are the most behaviour-shaping). Wound and fire behaviour should be tuned to *feel right* rather than statistically matched — gameplay readability matters here.
- If players find AI "too predictable" in disengagement, randomise disengagement thresholds within ±15% per pilot instance.
- If named-ace fights feel generic, prioritise the personality overlays (Stage 5) before adding more aircraft types.

## Caveats

- The "11 days life expectancy" figure (Hart, *Bloody April*) is specifically about new RFC subalterns during the April 1917 catastrophe. Across the whole war and all skill levels it was longer (months for veterans). Use as a "rookie risk" datum, not a universal one.
- Some popular WW1-air-combat tropes are mistaken or contested:
    - The "modern Immelmann" (half-loop + half-roll) is *not* what Immelmann or his contemporaries actually flew; the WW1 manoeuvre was a zoom-climb followed by a rudder yaw at the apex (Wikipedia and contemporary RNAS/RAF manuals are explicit on this).
    - The "Camel turns 270° right rather than 90° left" claim has been disputed by modern flight tests of an original Camel which show similar steady-state turn rates left and right (FLYING Magazine, *Calculated Sopwith Camel*); the roll-onset asymmetry is real, but the precise 270° claim is folk myth.
    - Lufbery's death (19 May 1918) is canonically described as a deliberate jump to avoid burning, but a 1962 USAF Museum investigation (Royal D. Frey) concluded he was thrown from the cockpit when the Nieuport flipped, not yet burning, after he unbuckled to clear a gun jam. The "jumped rather than burn" version is iconic but uncertain.
    - The Allied refusal to issue parachutes was *not*, on the evidence in the National Archives (per the Great War Aviation Society's investigation), a deliberate policy of "preserving fighting spirit" — that is the popular myth. The reality was a mix of technical immaturity (the available designs were heavy and static-line), bureaucratic inertia, and equipment-priority decisions. The effect on pilots was the same regardless.
- Richthofen's post-July-1917 brain-injury hypothesis (Allmers in *The Lancet* 1999; Hyatt and Orme 2004) is plausible but contested. Use it as one possible explanation for his uncharacteristic low-altitude pursuit on 21 April 1918, not as a certainty.
- Primary-source memoirs (Rickenbacker, Bishop, McCudden, Lewis, Lee, Udet, Richthofen, Grinnell-Milne) were all written for an audience and reflect propaganda, romance, or self-presentation pressures. Bishop's *Winged Warfare* in particular is now widely regarded by historians as containing inflated and unverifiable claims. The most reliable sources for combat-report data are squadron operational records and Jasta war diaries; for psychological data, the unpublished letters and diaries (Mannock's diary, Lee's private letters that became *No Parachute*) are more candid than the published memoirs.
- The Voss post-mortem details (wound pathway and timing) come via secondary sources (Franks, *Sharks Among Minnows*; Diggens, *September Evening*); the original RFC medical report is in the National Archives but not online.
- The Heinecke parachute one-third-failure statistic (Guttman, HistoryNet) cites the first 70 jumps; reliability improved later but never approached modern standards.

This note is intended as a working AI design document. Programmers should treat the numbers as starting points for playtesting, and historians should treat the behavioural model as a defensible synthesis rather than a record of any one pilot.

## Completion Table

| Research Item | Covered |
|---|---|
| Boelcke's Dicta — full text, numbered | ✓ Key Finding 1 |
| Named aces (Boelcke, Richthofen, Voss, Ball, Mannock, McCudden, Udet, Bishop, Rickenbacker, Grinnell-Milne) | ✓ Key Finding 2; §7 |
| Documented engagements (Hawker–Richthofen; Voss vs 56 Sqn; Mannock's death; Udet's jump; Ball's last flight) | ✓ §7 |
| Period memoirs (McCudden *Flying Fury*; Richthofen *Der rote Kampfflieger*; Mannock diary; Lewis *Sagittarius Rising*; Udet *Mein Fliegerleben*; Lee *Open Cockpit / No Parachute*; Rickenbacker *Fighting the Flying Circus*; Bishop *Winged Warfare*; Grinnell-Milne *Wind in the Wires*) | ✓ throughout |
| Biographies (Werner *Knight of Germany*; Gibbons *Red Knight of Germany*; Cole *McCudden VC*; Bowyer *Albert Ball VC*; Jones *King of Air Fighters*) | ✓ Key Finding 2 |
| Modern scholarship (Hart, Franks, VanWyngarden, Guttman, Diggens, Macmillan, Imrie) | ✓ Key Finding 2/4; §4.3; Caveats |
| "Fokker fever / Fokker Scourge" / "Hun in the sun" | ✓ Key Finding 3; §3.4 |
| Target fixation | ✓ §5.1; Richthofen Caveat |
| Hypoxia at altitude | ✓ §2.3; Key Finding 5 |
| Combat fatigue / "wind-up" / nerves | ✓ §5.1 |
| Ace mentality vs average pilot | ✓ §5.2, §5.3 |
| Wound effects (leg, arm, torso, head — grazing & severe) | ✓ §2.1 |
| Blood-loss / shock progression timing | ✓ §2.1 |
| Psychological reactions to being hit (panic, freeze, aggression, dive-for-home) | ✓ §2.1, §5.1 |
| Engine failure / dead-stick | ✓ §2.2 |
| Reactions to fire (jump, dive, suicide, parachute) | ✓ §2.2, Key Finding 6 |
| Wing/structural damage; control-surface loss | ✓ §2.2 |
| Out of ammunition behaviour | ✓ §2.2 |
| Single-pilot vs formation behaviour; isolated vs accompanied; outnumbered | ✓ §3.2, §3.5, §1.7, §2.2 |
| Lone-wolf → Boelcke wingman → Jasta → RFC patrol evolution | ✓ §3.1 |
| Specific tactics (sun dive, climbing turn, Immelmann reality, split-S, Lufbery, chandelle) | ✓ §1.6, §3.3 |
| Formation tactics (Kette, Jasta 12–14, Jagdgeschwader, RFC patrol of 6, Staffel) | ✓ §3.2 |
| Cloud cover, sun, altitude | ✓ §3.4 |
| Disengagement criteria | ✓ §3.5 |
| Energy management | ✓ §3.6 |
| Sopwith Camel characteristics (110 mph, 19,000 ft, twin Vickers, Clerget rotary, right-turn agility) | ✓ §4.1 |
| Fokker D.VII characteristics (117 mph, 19,700 ft, twin Spandau, BMW IIIa, "hung on prop") | ✓ §4.2 |
| Context aircraft (SE5a, SPAD XIII, Albatros D.III/D.V, Pfalz D.III, Fokker Dr.I) | ✓ §4.3 |
| Moral effect of aircraft type | ✓ §5.6 |
| Personal styles (Mannock careful/protective vs Ball solo vs Richthofen clinical vs Voss virtuoso vs Boelcke tactical vs McCudden high-altitude stalker) | ✓ Key Finding 2 |
| New-pilot-to-veteran progression; 11-day life expectancy | ✓ §5.1, Key Finding 4 |
| Aggression cycles | ✓ §5.1 |
| Risk tolerance — green ignorance vs veteran calculation vs broken fatalism | ✓ §5.2 |
| "Twitchiness" of veteran scan | ✓ §5.4 |
| Stalking and bouncing | ✓ §1.3, §1.4 |
| "I felt out of luck today" / refusal to engage | ✓ §5.5 |
| Skill tier differentiation | ✓ §5.3 |
| AI behavioural state mapping | ✓ §1, §2 |
| Numerical thresholds for programmer derivation | ✓ throughout |
| Communication realities (no radio, wing-rock, Very pistol) | ✓ §6 |
| Gun jams, cockpit hammer | ✓ §6, §2.2 |
| Cockpit equipment list (hammer, pistol, fire extinguisher, Very pistol, chocolate, POW haversack) | ✓ §6 |
| Castor oil / goggles / cold | ✓ §6 |
