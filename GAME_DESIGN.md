# Ascendant: oyun tasarımı

Unity 6 (6000.0.56f1), 2D pixel-art, **top-down aksiyon macera**. Level level ilerlenir, skiller sürekli gelişir.
(Önceki platformer hali `platformer-v0` etiketinde saklı.)

## Çekirdek döngü
1. Bir level'a gir (kısa zindan ya da arena, 5-10 dk).
2. Düşmanları yen, anahtar/şalter bul, kilitli kapıları aç.
3. Level sonu boss: yeni skill ya da mevcut skill için yeni kademe.
4. Skill puanı kazan, skill ağacında harca, güçlen.
5. Güçlenince eski level'lardaki kilitli yollar açılır, yeni level zorlaşır.

## Kamera ve hareket
- Üstten bakış, kamera oyuncuyu yumuşak takip eder; odalı level'larda oda geçişinde kayar.
- 8 yönlü hareket, normalize hız (çapraz hızlı değil). Yerçekimi yok.
- Sprite'lar 4 yönlü (aşağı, yukarı, sol/sağ; sağ = sol ayna).
- Fizik: Rigidbody2D (gravityScale 0), Collider2D, katmanlar: Player, Enemy, Wall, Hazard.

## Oyuncu
- Can: kalp sistemi (başlangıç 5 kalp, her kalp 2 parça).
- Hasar alınca kısa dokunulmazlık ve geri itme.
- **Stamina (mana):** ayrı bir bar. Dash, Büyü Topu ve diğer aktif skiller stamina harcar; stamina zamanla yavaşça dolar (kısa bir bekleme süresinden sonra). Asa vuruşu stamina harcamaz.
- Barlar HUD'da: kalpler/can barı ve stamina barı yan yana.
- **Düşman ödülü:** düşman ölünce şansa bağlı olarak can (kalp parçası) ya da stamina (mavi küre) bırakır. Boss daha çok bırakır. Toplanabilir pickup'lar oyuncuya doğru çekilir.
- Ölünce son checkpoint'ten devam, topladığı skill puanları kalır.

## Skiller
Her skill 3 kademelidir. Kademe 2 ve 3 skill puanıyla açılır.

| Skill | K1 | K2 | K3 | Başlangıç |
|---|---|---|---|---|
| Asa (büyülü savuruş) | 3 vuruş kombo, geri itme | Geniş savuruş | Dalga atan büyü | Açık |
| Dash | Kısa atılma, kısa dokunulmazlık | İz bırakır (hasar verir) | Işınlanır (duvardan geçer) | Level 2 boss |
| Büyü Topu | Ateş topu fırlatır | Delici/yanıcı top | Büyü yağmuru | Level 3 boss |
| Kalkan (L / sağ tık, basılı tut) | Ön yarım daireden gelen yakın dövüş vuruşunu engeller ve saldırgan geri itilir; -12 stamina/sn, yavaşlatır | Parry: ilk 0.2 sn'de engellenen vuruşun sahibi sersemler, +10 stamina | Parry sonrası sonraki Asa vuruşu x2 | Level 4 boss (test için açık) |
| Bomba (G, 25 stamina) | Yarıçap 2.0, çatlak duvarı yıkar, düşmana hasar (14-18) | Yarıçap 2.6, hasar 26-32 | Yarıçap 3.2, hasar 44-52, zincirleme patlama | Level 5 boss (test için açık) |
| Yay (R, 10 stamina) | Hızlı tek hedef oku (12-16) | Delici ok, 3 düşmana kadar (26-34) | Basılı tut + bırak: 8 oklu ok yağmuru, 30 stamina (46-56) | Level 6 boss (test için açık) |
| Fener (E, aç/kapa, -4 stamina/sn) | Yarıçap 4, `HiddenReveal` nesnelerini gösterir | Yarıçap 6, gizli duvarları açar, hafif hızlandırır | Yarıçap 8, açılırken flaş: düşmanlar 1.5 sn sersemler (8 sn bekleme) | Level 7 boss (test için açık) |

**Kombolar (iki skill art arda):** Dash + Asa = dönen kesik, Büyü Topu + Dash = iz bırakan top, Bomba + Ok = patlayan ok.

## Karakter ve sanat
- Oyuncu: kapüşonlu genç büyücü-savaşçı, elinde kristalli asa (4 yönlü, chibi, pixel-art).
- Üçüncü taraf sanat (Craftpix ücretsiz paketleri) `Assets/Art/ThirdParty` altında tutulur ve **git'e girmez** (lisans ham dosyaların dağıtımını yasaklıyor, repo public). Yeni bilgisayarda paketler yeniden indirilmelidir.

## Skill puanı ve ilerleme
- Puan kaynakları: düşman öldürme, level bitirme (yıldız sayısına göre), gizli oda.
- Skill ağacı UI'da, kademe açma kalıcıdır (JSON kayıt: `skills.json`, `progress.json`).
- Skill ağacı yalnızca level geçişlerinde (level seçme ve level sonu ekranı) açılır, oyun içinde açılmaz.
- Mevcut `SkillManager`, `SkillDefinition`, UI ve `ProgressService` kullanılmaya devam eder.

## Level yapısı
- Level = oda grafiği (3-8 oda) + tehlikeler + 1 boss odası.
- Kilitli kapı (anahtar), kilitli yol (skill gerekir), şalter, basınç plakası, diken.
- Her level 1-3 yıldız: süre, hasar almadan geçme, gizli oda.
- Level seçimi haritadan, tamamlanan level'lar yeniden oynanabilir.

## Düşmanlar
- Slime (yavaş yaklaşır), Yarasa (uçar, dalış atar), Okçu (uzaktan), Zırhlı (kalkan, arkadan vurulur), Boss (aşamalı).
- Hepsi `IDamageable` ve ortak `Health` bileşeni kullanır.

## Teknik kurallar
- Skript klasörleri: `Scripts/Player`, `Scripts/Skills`, `Scripts/Enemies`, `Scripts/Level`, `Scripts/UI`.
- Sanat: `Art/Player`, `Art/Environment`, `Art/Enemies`; PNG'ler `PixelArtImporter` ile otomatik Point/32 PPU/dilimli.
- Her oturum yalnızca kendi klasörlerine yazar, commit'i yönetici oturumu atar.
- Aynı anda yalnızca bir oturum Unity'de sahne açar veya Play'e girer.
- Her skill `SkillAbility`'den türer, `SkillManager` üzerinden kilitli/açık yönetilir.

## Eski platformer kodundan neyin değişeceği
- Gider: yerçekimi, zıplama, duvar kayma, çift zıplama, yukarı kayan kamera, yükselen lav, yan görünüm tileset'i.
- Yeniden yazılır: oyuncu hareketi (8 yön), animasyon sürücüsü, oyuncu ve düşman sprite'ları (4 yönlü, üstten), kanca (yön tabanlı).
- Kalır: skill altyapısı, UI, ilerleme kaydı, tile mantığı, prefab düzeni.

## Savaş akışı ve hasar (hızlı akış)
- Düşmanlar çok ve kolay ölür. Düşman canı "can puanı" (Orc1 20, Orc2 24, Orc3 40; `EnemyStats.maxHealth`). Oyuncunun kalp sistemi ayrıdır, düşman hasarı oyuncuya kalp parçası olarak biner.
- Oyuncu skill hasarı kademeye göre aralıktır (`SkillDefinition.tierDamage`): Asa K1 9-13, K2 22-28, K3 40-48; Büyü Topu K1 8-12, K2 18-24, K3 36-44. K1'de Orc1 ~2 vuruş, K2'de çoğu düşman 1 vuruş.
- Her vuruş: taban × rastgele 0.85-1.2, %12 ihtimalle kritik ×1.6 (`DamageRoll`). Asa kombosunun 3. vuruşu ×1.4. Kademe `SkillManager.GetTier/SetTier` ile tutulur (skill ağacı kademe açınca çağırır).
- Düşmana hasar verilince yüzen sayı çıkar (beyaz, kritik büyük turuncu, Büyü Topu mavi).
- Test: `EnemySpawner` oyuncu çevresinde ekran dışında düşman üretir (en fazla 15).

## Arayüz
- Tek görsel dil: koyu mor-lacivert zemin, altın/turuncu vurgu, can kırmızı, stamina mavi, açık krem metin. Hepsi `Assets/Art/UI/Theme/UITheme.asset` içinde (renkler, boyutlar, font, 9-slice sprite'lar); renk/boyut değişince `Tools/UI/Rebuild All` ile prefab ve sahneler yeniden üretilir.
- Font: kodla çizilen bitmap pixel font (`PixelUI`, kalın hali `PixelUIBold`; Türkçe karakterler dahil, `Tools/UI/Build Pixel Font`). Font boyutları 12'nin katı olmalı (24/36/48/72), yoksa pikseller bozulur. Hasar sayıları ve pickup yazıları eski `PixelFont.asset` ile kalır.
- Butonlar `UIButtonFx` ile hover 1.06 / basınç 0.96 ölçek alır; klavye/gamepad seçimi aynı vurguyu ve yan okları gösterir. Sahne geçişleri `SceneRouter` üzerinden 0.25 sn siyaha kararır (`ScreenFade`).
- HUD (sol üst): kalpler, stamina barı (düşükken kırmızıya döner ve titrer), açık skill slotları (tuş harfi, bekleme sayacı, stamina yetmezken karartma); sağ üstte süre ve skill puanı; boss odasında üstte `BossHealthBar` (`EnemyStats.isBoss` işaretli düşman doğunca `RoomController.EnemySpawned` ile açılır, boss ölünce kapanır).
- Esc duraklatma menüsü (`PauseMenu`): Devam, Level Seç, Ana Menü. Skill ağacı burada yok; yalnızca level seçme ve level sonu ekranından açılır. Skill ağacı veya skor ekranı açıkken Esc duraklatmayı açmaz.
- Ana menü ve level seçimi parallax mağara arka planı ve süzülen kıvılcımlarla (`MenuBackground`); skor ekranında sayılar sayarak artar, yıldızlar sırayla yanar.
