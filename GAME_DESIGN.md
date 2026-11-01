# Ascendant: oyun tasarÄ±mÄ±

Unity 6 (6000.0.56f1), 2D pixel-art, **top-down aksiyon macera**. Level level ilerlenir, skiller sÃ¼rekli geliÅŸir.
(Ã–nceki platformer hali `platformer-v0` etiketinde saklÄ±.)

## Ã‡ekirdek dÃ¶ngÃ¼
1. Bir level'a gir (kÄ±sa zindan ya da arena, 5-10 dk).
2. DÃ¼ÅŸmanlarÄ± yen, anahtar/ÅŸalter bul, kilitli kapÄ±larÄ± aÃ§.
3. Level sonu boss: yeni skill ya da mevcut skill iÃ§in yeni kademe.
4. Skill puanÄ± kazan, skill aÄŸacÄ±nda harca, gÃ¼Ã§len.
5. GÃ¼Ã§lenince eski level'lardaki kilitli yollar aÃ§Ä±lÄ±r, yeni level zorlaÅŸÄ±r.

## Kamera ve hareket
- Ãœstten bakÄ±ÅŸ, kamera oyuncuyu yumuÅŸak takip eder; odalÄ± level'larda oda geÃ§iÅŸinde kayar.
- 8 yÃ¶nlÃ¼ hareket, normalize hÄ±z (Ã§apraz hÄ±zlÄ± deÄŸil). YerÃ§ekimi yok.
- Sprite'lar 4 yÃ¶nlÃ¼ (aÅŸaÄŸÄ±, yukarÄ±, sol/saÄŸ; saÄŸ = sol ayna).
- Fizik: Rigidbody2D (gravityScale 0), Collider2D, katmanlar: Player, Enemy, Wall, Hazard, Anchor.

## Oyuncu
- Can: kalp sistemi (baÅŸlangÄ±Ã§ 5 kalp, her kalp 2 parÃ§a).
- Hasar alÄ±nca kÄ±sa dokunulmazlÄ±k ve geri itme.
- Ã–lÃ¼nce son checkpoint'ten devam, topladÄ±ÄŸÄ± skill puanlarÄ± kalÄ±r.

## Skiller
Her skill 3 kademelidir. Kademe 2 ve 3 skill puanÄ±yla aÃ§Ä±lÄ±r.

| Skill | K1 | K2 | K3 | BaÅŸlangÄ±Ã§ |
|---|---|---|---|---|
| KÄ±lÄ±Ã§ | 3 vuruÅŸ kombo, geri itme | GeniÅŸ savuruÅŸ | Dalga atan kÄ±lÄ±Ã§ | AÃ§Ä±k |
| Dash | KÄ±sa atÄ±lma, kÄ±sa dokunulmazlÄ±k | Ä°z bÄ±rakÄ±r (hasar verir) | IÅŸÄ±nlanÄ±r (duvardan geÃ§er) | Level 2 boss |
| Kanca | Anchor'a Ã§eker | DÃ¼ÅŸmanÄ± Ã§eker/sersemletir | Ä°ki anchor arasÄ± salÄ±nÄ±m | Level 3 boss |
| Kalkan | Mermiyi engeller | Mermiyi yansÄ±tÄ±r | Parry sonrasÄ± gÃ¼Ã§lÃ¼ vuruÅŸ | Level 4 boss |
| Bomba | Ã‡atlak duvarÄ± yÄ±kar | Alan hasarÄ± | Zincirleme patlama | Level 5 boss |
| Yay | Uzak ÅŸalter/dÃ¼ÅŸman | Delici ok | Ok yaÄŸmuru | Level 6 boss |
| Fener | KaranlÄ±ÄŸÄ± aydÄ±nlatÄ±r | GÃ¶rÃ¼nmez yolu gÃ¶sterir | DÃ¼ÅŸmanÄ± kÃ¶r eder | Level 7 boss |

**Kombolar (iki skill art arda):** Dash + KÄ±lÄ±Ã§ = dÃ¶nen kesik, Kanca + KÄ±lÄ±Ã§ = Ã§ekip vuruÅŸ, Bomba + Ok = patlayan ok.

## Skill puanÄ± ve ilerleme
- Puan kaynaklarÄ±: dÃ¼ÅŸman Ã¶ldÃ¼rme, level bitirme (yÄ±ldÄ±z sayÄ±sÄ±na gÃ¶re), gizli oda.
- Skill aÄŸacÄ± UI'da, kademe aÃ§ma kalÄ±cÄ±dÄ±r (JSON kayÄ±t: `skills.json`, `progress.json`).
- Mevcut `SkillManager`, `SkillDefinition`, UI ve `ProgressService` kullanÄ±lmaya devam eder.

## Level yapÄ±sÄ±
- Level = oda grafiÄŸi (3-8 oda) + tehlikeler + 1 boss odasÄ±.
- Kilitli kapÄ± (anahtar), kilitli yol (skill gerekir), ÅŸalter, basÄ±nÃ§ plakasÄ±, diken.
- Her level 1-3 yÄ±ldÄ±z: sÃ¼re, hasar almadan geÃ§me, gizli oda.
- Level seÃ§imi haritadan, tamamlanan level'lar yeniden oynanabilir.

## DÃ¼ÅŸmanlar
- Slime (yavaÅŸ yaklaÅŸÄ±r), Yarasa (uÃ§ar, dalÄ±ÅŸ atar), OkÃ§u (uzaktan), ZÄ±rhlÄ± (kalkan, arkadan vurulur), Boss (aÅŸamalÄ±).
- Hepsi `IDamageable` ve ortak `Health` bileÅŸeni kullanÄ±r.

## Teknik kurallar
- Skript klasÃ¶rleri: `Scripts/Player`, `Scripts/Skills`, `Scripts/Enemies`, `Scripts/Level`, `Scripts/UI`.
- Sanat: `Art/Player`, `Art/Environment`, `Art/Enemies`; PNG'ler `PixelArtImporter` ile otomatik Point/32 PPU/dilimli.
- Her oturum yalnÄ±zca kendi klasÃ¶rlerine yazar, commit'i yÃ¶netici oturumu atar.
- AynÄ± anda yalnÄ±zca bir oturum Unity'de sahne aÃ§ar veya Play'e girer.
- Her skill `SkillAbility`'den tÃ¼rer, `SkillManager` Ã¼zerinden kilitli/aÃ§Ä±k yÃ¶netilir.

## Eski platformer kodundan neyin deÄŸiÅŸeceÄŸi
- Gider: yerÃ§ekimi, zÄ±plama, duvar kayma, Ã§ift zÄ±plama, yukarÄ± kayan kamera, yÃ¼kselen lav, yan gÃ¶rÃ¼nÃ¼m tileset'i.
- Yeniden yazÄ±lÄ±r: oyuncu hareketi (8 yÃ¶n), animasyon sÃ¼rÃ¼cÃ¼sÃ¼, oyuncu ve dÃ¼ÅŸman sprite'larÄ± (4 yÃ¶nlÃ¼, Ã¼stten), kanca (yÃ¶n tabanlÄ±).
- KalÄ±r: skill altyapÄ±sÄ±, UI, ilerleme kaydÄ±, tile mantÄ±ÄŸÄ±, prefab dÃ¼zeni.


## Karakter ve sanat
- Oyuncu: kapüşonlu genç büyücü-savaşçı, elinde kristalli asa (4 yönlü, chibi, pixel-art).
- Üçüncü taraf sanat (Craftpix ücretsiz paketleri) Assets/Art/ThirdParty altında tutulur ve **git'e girmez** (lisans ham dosyaların dağıtımını yasaklıyor, repo public). Yeni bilgisayarda paketler yeniden indirilmelidir.
