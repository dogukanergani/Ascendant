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
| Kalkan | Mermiyi engeller | Mermiyi yansıtır | Parry sonrası güçlü vuruş | Level 4 boss |
| Bomba | Çatlak duvarı yıkar | Alan hasarı | Zincirleme patlama | Level 5 boss |
| Yay | Uzak şalter/düşman | Delici ok | Ok yağmuru | Level 6 boss |
| Fener | Karanlığı aydınlatır | Görünmez yolu gösterir | Düşmanı kör eder | Level 7 boss |

**Kombolar (iki skill art arda):** Dash + Asa = dönen kesik, Büyü Topu + Dash = iz bırakan top, Bomba + Ok = patlayan ok.

## Karakter ve sanat
- Oyuncu: kapüşonlu genç büyücü-savaşçı, elinde kristalli asa (4 yönlü, chibi, pixel-art).
- Üçüncü taraf sanat (Craftpix ücretsiz paketleri) `Assets/Art/ThirdParty` altında tutulur ve **git'e girmez** (lisans ham dosyaların dağıtımını yasaklıyor, repo public). Yeni bilgisayarda paketler yeniden indirilmelidir.

## Skill puanı ve ilerleme
- Puan kaynakları: düşman öldürme, level bitirme (yıldız sayısına göre), gizli oda.
- Skill ağacı UI'da, kademe açma kalıcıdır (JSON kayıt: `skills.json`, `progress.json`).
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
