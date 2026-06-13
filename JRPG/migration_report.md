# Migration Report

> **Status: Historical discarded-data report.** This report describes the abandoned legacy-to-v2 dataset conversion. It is retained only as migration evidence and must not define the redesign schema, vocabulary, or content requirements.

## Summary
- Total skills converted successfully: 410
- Total entities converted successfully: 304
- Skills omitted because they could not be mapped: 10
- Mapper warnings recorded: 6
- Entities with missing legacy InheritanceType: 304 (all derived in `entity_database_v2.json`)
- Unresolved entity skill references omitted: 203

## Skill Mapper Warnings
- Pestilence: Ailment 'Poison' was inferred without an explicit chance; defaulting to 100.
- Terror Blade: Ailment 'Panic' was inferred without an explicit chance; defaulting to 100.
- Dark Sword: Ailment 'Fear' was inferred without an explicit chance; defaulting to 100.
- Guillotine: Ailment 'Stun' was inferred without an explicit chance; defaulting to 100.
- Stasis Blade: Ailment 'Stun' was inferred without an explicit chance; defaulting to 100.
- Weary Thrust: Ailment 'Panic' was inferred without an explicit chance; defaulting to 100.

## Skills Not Converted
- Dismal Tune: Damage skill 'Dismal Tune' is missing numeric accuracy.
- Baisudi: Healing skill 'Baisudi' has no inferable recovery amount.
- Paraladi: Healing skill 'Paraladi' has no inferable recovery amount.
- Patra: Healing skill 'Patra' has no inferable recovery amount.
- Re Patra: Healing skill 'Re Patra' has no inferable recovery amount.
- Me Patra: Healing skill 'Me Patra' has no inferable recovery amount.
- Posumudi: Healing skill 'Posumudi' has no inferable recovery amount.
- Charmdi: Healing skill 'Charmdi' has no inferable recovery amount.
- Enradi: Healing skill 'Enradi' has no inferable recovery amount.
- Amrita: Healing skill 'Amrita' has no inferable recovery amount.

## Entities Missing InheritanceType
None. The legacy file still contains no explicit `InheritanceType` values, but every v2 entity now has a derived value based on non-weak affinities and skill evidence.

## Inheritance Type Derivation
Rule used: score base and learned elemental skill evidence first, ignore any candidate element the entity is Weak to, then fall back to Absorb/Repel/Null/Resist affinities, and finally use the dominant attack stat only when no elemental evidence exists.

Source counts:
- affinity strength: 12
- skill evidence: 289
- stat fallback: 3

Assigned type counts:
- Almighty: 10
- Curse: 1
- Dark: 16
- Earth: 1
- Elec: 32
- Fire: 42
- Ice: 32
- Light: 26
- Mind: 21
- Nerve: 3
- Pierce: 25
- Slash: 32
- Strike: 24
- Wind: 39

Low-confidence assignments:
- Caesar (`caesar`): `Slash` via stat fallback (St 10 >= Ma 8).
- Lucia (`lucia`): `Fire` via stat fallback (Ma 11 > St 4).
- Juno (`juno`): `Fire` via stat fallback (Ma 11 > St 4).

Weak-element evidence ignored:
- Barong (`barong`): ignored learned skill `null_dark` as `Dark` evidence because the entity is Weak to Dark.
- Jatayu (`jatayu`): ignored learned skill `evade_pierce` as `Pierce` evidence because the entity is Weak to Pierce.
- Houou (`houou`): ignored learned skill `null_pierce` as `Pierce` evidence because the entity is Weak to Pierce.
- Badb Catha (`badb_catha`): ignored base skill `needle_rush` as `Pierce` evidence because the entity is Weak to Pierce.
- Badb Catha (`badb_catha`): ignored base skill `needle_rush` as `Pierce` evidence because the entity is Weak to Pierce.
- Shiki-Ouji (`shiki_ouji`): ignored learned skill `evade_wind` as `Wind` evidence because the entity is Weak to Wind.
- Shiki-Ouji (`shiki_ouji`): ignored learned skill `null_fire` as `Fire` evidence because the entity is Weak to Fire.
- Saturnus (`saturnus`): ignored learned skill `repel_ice` as `Ice` evidence because the entity is Weak to Ice.
- Power (`power`): ignored base skill `dark_might` as `Dark` evidence because the entity is Weak to Dark.
- Power (`power`): ignored learned skill `null_nerve` as `Nerve` evidence because the entity is Weak to Nerve.
- Ananta (`ananta`): ignored learned skill `evade_slash` as `Slash` evidence because the entity is Weak to Slash.
- Alilat (`alilat`): ignored learned skill `repel_fire` as `Fire` evidence because the entity is Weak to Fire.
- Alilat (`alilat`): ignored learned skill `inferno` as `Fire` evidence because the entity is Weak to Fire.
- Tam Lin (`tam_lin`): ignored learned skill `survive_dark` as `Dark` evidence because the entity is Weak to Dark.
- Jack-o'-Lantern (`jack_o_lantern`): ignored learned skill `dodge_ice` as `Ice` evidence because the entity is Weak to Ice.
- Jack-o'-Lantern (Rank 8) (`jack_o_lantern_rank_8`): ignored learned skill `resist_ice` as `Ice` evidence because the entity is Weak to Ice.
- Hua Po (`hua_po`): ignored learned skill `resist_freeze` as `Ice` evidence because the entity is Weak to Ice.
- High Pixie (Rank 10) (`high_pixie_rank_10`): ignored learned skill `dodge_wind` as `Wind` evidence because the entity is Weak to Wind.
- Setanta (`setanta`): ignored learned skill `null_curse` as `Curse` evidence because the entity is Weak to Curse.
- Forneus (`forneus`): ignored learned skill `resist_elec` as `Elec` evidence because the entity is Weak to Elec.
- Satan (`satan`): ignored learned skill `repel_wind` as `Wind` evidence because the entity is Weak to Wind.
- Pale Rider (`pale_rider`): ignored learned skill `survive_light` as `Light` evidence because the entity is Weak to Light.
- White Rider (`white_rider`): ignored base skill `agilao` as `Fire` evidence because the entity is Weak to Fire.
- White Rider (`white_rider`): ignored base skill `agilao` as `Fire` evidence because the entity is Weak to Fire.
- White Rider (`white_rider`): ignored learned skill `fire_amp` as `Fire` evidence because the entity is Weak to Fire.
- White Rider (`white_rider`): ignored learned skill `agidyne` as `Fire` evidence because the entity is Weak to Fire.
- White Rider (`white_rider`): ignored learned skill `agidyne` as `Fire` evidence because the entity is Weak to Fire.
- Alice (`alice`): ignored learned skill `endure_light` as `Light` evidence because the entity is Weak to Light.
- Will o' Wisp (`will_o_wisp`): ignored learned skill `garu` as `Wind` evidence because the entity is Weak to Wind.
- Will o' Wisp (`will_o_wisp`): ignored learned skill `garu` as `Wind` evidence because the entity is Weak to Wind.
- Susano-o (`susano_o`): ignored learned skill `repel_light` as `Light` evidence because the entity is Weak to Light.
- Polydeuces (`polydeuces`): ignored learned skill `evade_ice` as `Ice` evidence because the entity is Weak to Ice.
- Robin Hood (`robin_hood`): ignored base skill `eiga` as `Dark` evidence because the entity is Weak to Dark.
- Robin Hood (`robin_hood`): ignored learned skill `eigaon` as `Dark` evidence because the entity is Weak to Dark.
- Preta (`preta`): ignored learned skill `agi` as `Fire` evidence because the entity is Weak to Fire.
- Preta (`preta`): ignored learned skill `agi` as `Fire` evidence because the entity is Weak to Fire.
- Shiisaa (`shiisaa`): ignored learned skill `resist_fire` as `Fire` evidence because the entity is Weak to Fire.
- Chimera (`chimera`): ignored learned skill `sonic_wave` as `Mind` evidence because the entity is Weak to Mind.
- Chimera (`chimera`): ignored learned skill `sonic_wave` as `Mind` evidence because the entity is Weak to Mind.
- Take-Minakata (Rank 2) (`take_minakata_rank_2`): ignored learned skill `survive_dark` as `Dark` evidence because the entity is Weak to Dark.
- Attis (`attis`): ignored base skill `mahamaon` as `Light` evidence because the entity is Weak to Light.
- Skadi (`skadi`): ignored learned skill `repel_elec` as `Elec` evidence because the entity is Weak to Elec.
- Incubus (`incubus`): ignored base skill `magarula` as `Wind` evidence because the entity is Weak to Wind.
- Incubus (`incubus`): ignored base skill `magarula` as `Wind` evidence because the entity is Weak to Wind.
- Black Frost (`black_frost`): ignored learned skill `resist_light` as `Light` evidence because the entity is Weak to Light.
- Kaiwan (`kaiwan`): ignored learned skill `null_light` as `Light` evidence because the entity is Weak to Light.
- Naga Raja (`naga_raja`): ignored learned skill `null_fire` as `Fire` evidence because the entity is Weak to Fire.
- Belphegor (`belphegor`): ignored learned skill `endure_light` as `Light` evidence because the entity is Weak to Light.
- Arahabaki (`arahabaki`): ignored learned skill `sonic_wave` as `Mind` evidence because the entity is Weak to Mind.
- Arahabaki (`arahabaki`): ignored learned skill `sonic_wave` as `Mind` evidence because the entity is Weak to Mind.
- Nidhoggr (`nidhoggr`): ignored learned skill `null_light` as `Light` evidence because the entity is Weak to Light.
- Seth (`seth`): ignored learned skill `repel_ice` as `Ice` evidence because the entity is Weak to Ice.
- Mothman (`mothman`): ignored learned skill `null_elec` as `Elec` evidence because the entity is Weak to Elec.
- Mothman (`mothman`): ignored learned skill `absorb_earth` as `Earth` evidence because the entity is Weak to Earth.
- Narcissus (`narcissus`): ignored learned skill `dodge_slash` as `Slash` evidence because the entity is Weak to Slash.
## Unresolved Entity Skill References
These references were omitted from `baseSkillIds` or `learnedSkillIds` because no converted skill ID exists.
- Ailment Boost (`ailment_boost`): magatsu_izanagi:learned@52, penthesilea:learned@63, nidhoggr:learned@51
- Ali Dance (`ali_dance`): vanadis:learned@64, futsunushi:learned@76, kikuri_hime_rank_13:base
- Amrita Drop (`amrita_drop`): seiryu:learned@41, io:learned@46, milady:base, ganga:learned@47
- Amrita Shower (`amrita_shower`): jatayu:learned@59, io:learned@62, hariti:learned@52, astarte:learned@56, kaguya:learned@38
- Analysis Mastery (`analysis_mastery`): lucia:base, lucia:learned@51
- Analysis Perfection (`analysis_perfection`): lucia:learned@65
- Analysis Plus (`analysis_plus`): lucia:base, lucia:learned@40
- Angelic Grace (`angelic_grace`): sandalphon:learned@81
- Anti-Ice Master (`anti_ice_master`): genbu:learned@33
- Auto Rebellion (`auto_rebellion`): seiten_taisei:learned@41
- Baisudi (`baisudi`): silky:learned@7, hua_po:base
- Berserk (`berserk`): troll:learned@39
- Blazing Hell (`blazing_hell`): hecate:base
- Complete Support (`complete_support`): lucia:learned@70
- Concentrate (`concentrate`): thoth:learned@45, saturnus:base, kohryu:learned@73, satan:learned@84, penthesilea:learned@55, hecate:learned@46
- Confuse Boost (`confuse_boost`): ganga:base, sandman:learned@24, naga_raja_rank_7:learned@46
- Crit Rate Amp (`crit_rate_amp`): masakado:learned@83
- Cross Slash (`cross_slash`): izanagi:base
- Dark Amp (`dark_amp`): thanatos:learned@79, chernobog:learned@60
- Dark Boost (`dark_boost`): black_frost:learned@39
- Death Lust (`death_lust`): mother_harlot:base
- Diara (`diara`): ganga:base
- Dismal Tune (`dismal_tune`): berith:learned@40, dionysus:learned@45
- Divine Judgement (`divine_judgement`): cu_chulainn_rank_14:learned@54, kresnik:learned@54
- Dodge Light (`dodge_light`): belphegor:base
- Drain Dark (`drain_dark`): sandalphon:learned@87
- Drain Fire (`drain_fire`): hecate:learned@48
- Drain Light (`drain_light`): mara:learned@82
- Drain Slash (`drain_slash`): ananta:learned@80
- Drain Strike (`drain_strike`): chi_you:learned@93
- Enradi (`enradi`): xiezhai:learned@29
- Evade Electric (`evade_electric`): mercurius:learned@47
- Evade Light (`evade_light`): hell_biker_rank_10:learned@70
- Fire Repel (`fire_repel`): thor:learned@80
- Firm Stance (`firm_stance`): asura:learned@91, castor:learned@70
- Focus (`focus`): momunofu:base, kin_ki:base, setanta:learned@44, ose:learned@48, matador:learned@32, byakko:learned@45, sarutahiko:learned@38, take_minakata:learned@21, futomimi:base, arahabaki:base
- Freeze Boost (`freeze_boost`): jack_frost:learned@12, jack_frost_rank_4:learned@12, penthesilea:learned@47, king_frost:base
- Full Analysis (`full_analysis`): lucia:base, lucia:learned@18
- Full Support (`full_support`): lucia:learned@48
- Growth (`growth`): gurulu:base, pulukishi:base
- High Healing Wave (`high_healing_wave`): lucia:learned@55
- Highcounter (`highcounter`): thor:base
- Ice Drain (`ice_drain`): quetzalcoatl:learned@58
- Jamming (`jamming`): lucia:learned@4
- Life Bonus (`life_bonus`): momunofu:learned@21, aquans:learned@17, ame_no_uzume:learned@21, fomorian:learned@20
- Life Refill (`life_refill`): unicorn:learned@22, take_mikazuchi_rank_7:learned@47, ara_mitama:learned@27
- Light Amp (`light_amp`): nemesis:learned@72, cybele:learned@71
- Mana Bonus (`mana_bonus`): nekomata:learned@19, principality:learned@30, koumokuten:learned@35
- Mana Refill (`mana_refill`): ame_no_uzume:learned@23, sarasvati:base
- Maragilao (`maragilao`): orobas:learned@40
- Marziodyne (`marziodyne`): thor:learned@78
- Matera (`matera`): erthrys:learned@10, suzaku:base, koumokuten:base, ame_no_uzume:learned@19, nozuchi:learned@17
- Materadyne (`materadyne`): parvati:learned@63
- Materazi (`materazi`): yomotsu_ikusa:learned@47, troll:base, yomotsu_shikome:learned@33, gogmagog:base, jikokuten:learned@57, kushinada:learned@44, naga_raja:learned@41
- Mazandyne (`mazandyne`): trumpeter:base
- Me Patra (`me_patra`): makami:learned@24, yomotsu_ikusa:base, high_pixie:learned@12, high_pixie_rank_10:learned@24, io:learned@33, leanan_sidhe:learned@24, queen_medb:base, unicorn:learned@25, sarutahiko:learned@36, kikuri_hime:learned@26, hariti:base, mizuchi:learned@36, dis:learned@26
- Medical Scan (`medical_scan`): lucia:learned@15
- Mutudi (`mutudi`): troll:base
- Null Confuse (`null_confuse`): genbu:base, sandman:learned@21
- Null Phys (`null_phys`): messiah:learned@96
- One-shot Kill (`one_shot_kill`): astarte:base
- Oracle Mastery (`oracle_mastery`): lucia:learned@37
- Paraladi (`paraladi`): nekomata:base, kelpie:base, kushinada:learned@43
- Patra (`patra`): xiezhai:base, angel:learned@12, erthrys:base, io:learned@5, datsue_ba:learned@9, leanan_sidhe:base, hua_po_rank_2:learned@6, fortuna:base, apsaras:base
- Petradi (`petradi`): ame_no_uzume:learned@22
- Pierec Amp (`pierec_amp`): kresnik:learned@57
- Posumudi (`posumudi`): angel:learned@13, pixie:learned@5, kikuri_hime:learned@25, zhen:base
- Prayer (`prayer`): amaterasu:learned@60, vishnu:base, titania:learned@62, raphael:learned@85
- Resist Ailments (`resist_ailments`): ananta:learned@75, nebiros:learned@53, satan:base, robin_hood:learned@40
- Resist Dizzy (`resist_dizzy`): neko_shogun:learned@22
- Shift Amp (`shift_amp`): chi_you:base
- Shock Boost (`shock_boost`): thunderbird:base, captain_kidd:learned@20, polydeuces:learned@17
- Soul Chain (`soul_chain`): vanadis:learned@59
- Soul Link (`soul_link`): norn:learned@70
- Soul Shift (`soul_shift`): thoth:learned@43, izanagi:learned@52
- Stun Bite (`stun_bite`): kelpie:learned@27
- Stun Gaze (`stun_gaze`): jack_o_lantern_rank_8:learned@20, chatterskull:base, pisaca:learned@29, titan:learned@50, bicorn:learned@20
- Support Mastery (`support_mastery`): lucia:learned@21
- Survival Trick (`survival_trick`): arsene:learned@29, attis:learned@74, satanael:base
- Tactical Mastery (`tactical_mastery`): lucia:learned@33
- Tartarus Mastery (`tartarus_mastery`): lucia:learned@60
- Tartarus Search (`tartarus_search`): lucia:learned@44
- Tera (`tera`): inugami:base, nekomata:base, erthrys:base, datsue_ba:base, blob:base, zorro:base, sudama:learned@14, ame_no_uzume:base, nozuchi:base
- Teradyne (`teradyne`): atropos:base, okuninushi:learned@43, parvati:learned@62
- Terazi (`terazi`): yomotsu_ikusa:base, dakini:base, zouchouten:learned@29, jikokuten:base, kushinada:base, naga_raja:base, pulukishi:base
- Vacuum Slash (`vacuum_slash`): arsene:learned@25, hermes:learned@13
- Wild Thunder (`wild_thunder`): seiten_taisei:base

## Duplicate Skill Adjustments
- Life Aid in Passive Skills: `life_aid` adjusted to `life_aid_passive` with display name `Life Aid (Passive)`.
- Feral Claw in Slash Skills: `feral_claw` adjusted to `feral_claw_slash` with display name `Feral Claw (Slash)`.
- Trafuri in Special Skills: `trafuri` adjusted to `trafuri_special` with display name `Trafuri (Special)`.

## Duplicate Entity Adjustments
- Shiki-Ouji (`shiki_ouji`): `shiki_ouji` adjusted to `shiki_ouji_rank_7` with display name `Shiki-Ouji (Rank 7)`.
- Jack Frost (`jack-frost`): `jack_frost` adjusted to `jack_frost_rank_4` with display name `Jack Frost (Rank 4)`.
- Jack-o'-Lantern (`jack_o_lantern`): `jack_o_lantern` adjusted to `jack_o_lantern_rank_8` with display name `Jack-o'-Lantern (Rank 8)`.
- High Pixie (`high-pixie`): `high_pixie` adjusted to `high_pixie_rank_10` with display name `High Pixie (Rank 10)`.
- White Rider (`white_rider`): `white_rider` adjusted to `white_rider_rank_6` with display name `White Rider (Rank 6)`.
- Pale Rider (`pale_rider`): `pale_rider` adjusted to `pale_rider_rank_9` with display name `Pale Rider (Rank 9)`.
- Hell Biker (`hell-biker`): `hell_biker` adjusted to `hell_biker_rank_10` with display name `Hell Biker (Rank 10)`.
- Mother Harlot (`mother-harlot`): `mother_harlot` adjusted to `mother_harlot_rank_14` with display name `Mother Harlot (Rank 14)`.
- Qitian Dasheng (`qitian-dasheng`): `qitian_dasheng` adjusted to `qitian_dasheng_rank_5` with display name `Qitian Dasheng (Rank 5)`.
- Kurama Tengu (`kurama-tengu`): `kurama_tengu` adjusted to `kurama_tengu_rank_13` with display name `Kurama Tengu (Rank 13)`.
- Cu Chulainn (`cu_chulainn`): `cu_chulainn` adjusted to `cu_chulainn_rank_14` with display name `Cu Chulainn (Rank 14)`.
- Hua Po (`hua_po`): `hua_po` adjusted to `hua_po_rank_2` with display name `Hua Po (Rank 2)`.
- Take-Minakata (`take-minakata`): `take_minakata` adjusted to `take_minakata_rank_2` with display name `Take-Minakata (Rank 2)`.
- Take-Mikazuchi (`take_mikazuchi`): `take_mikazuchi` adjusted to `take_mikazuchi_rank_7` with display name `Take-Mikazuchi (Rank 7)`.
- Kikuri-Hime (`kikuri-hime`): `kikuri_hime` adjusted to `kikuri_hime_rank_13` with display name `Kikuri-Hime (Rank 13)`.
- Black Frost (`black_frost`): `black_frost` adjusted to `black_frost_rank_14` with display name `Black Frost (Rank 14)`.
- Naga Raja (`naga-raja`): `naga_raja` adjusted to `naga_raja_rank_7` with display name `Naga Raja (Rank 7)`.

## Other Anomalies
- Skill 'Fire Break' has non-canonical cost '40 SP*'; migrated as 40 SP.
- Skill 'Ice Break' has non-canonical cost '40 SP*'; migrated as 40 SP.
- Skill 'Elec Break' has non-canonical cost '40 SP*'; migrated as 40 SP.
- Skill 'Wind Break' has non-canonical cost '40 SP*'; migrated as 40 SP.

