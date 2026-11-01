# Level'i Tile Palette ile elle boyama

Craftpix "2D Top-Down Dungeon Tileset" Unity Tile Palette'lerine çevrildi. `Level_01` sahnesi boyanmaya hazır; otomatik üretilmiş eski hali `Assets/Scenes/Level_01_Auto.unity` içinde duruyor (Build Settings'te değil, sadece yedek).

> Ham PNG'ler `Assets/Art/ThirdParty/` altında ve git dışı. Palette/tile asset'leri `Assets/Art/Environment/TopDown/` altında git'te; başka bilgisayarda çalışmaları için paket orada da açılmış olmalı.

## 1. Tile Palette penceresini aç

`Window > 2D > Tile Palette`. Pencerede üstteki açılır listeden palette seç, **Active Tilemap** olarak aşağıda anlatılan Tilemap'i seç (pencerenin üstünde, ya da Hierarchy'de Tilemap'e tıklayınca otomatik).

Fırça araçları (üst çubuk): **Select**, **Move**, **Paint (B)**, **Box Fill (U)**, **Pick (I)**, **Erase (D)**, **Flood Fill (G)**. Seçili tile(lar) fırça olur; palette'ten birden çok tile seçip blok olarak damgalayabilirsin (kapı kemeri gibi).

## 2. Palette'ler (`Assets/Art/Environment/TopDown/Palettes/`)

| Palette | Boyanacak Tilemap | İçerik |
|---|---|---|
| `Dungeon_Ground` | **Ground** | `Floor_Auto` (+ `_Boss` kırmızı tema), 16'lık zemin ve gölgeli zemin setleri (+ boss varyantları) |
| `Dungeon_Walls` | **Walls** (collider'lı) ya da **WallTop** (collider'sız) | `Wall_Auto` (+ `_Boss`), paketin duvar sayfaları (üst duvar, sağ/sol uç, düz kaya dolgu, blok sütun, kapak şeritleri, post), kemerli kapı |
| `Dungeon_Decor` | **Decor** | çatlak, slime, dikenler/tuzaklar, altın, anahtar, mücevher, ot/mercan, taş çakılları |
| `Dungeon_Props` | **Props** | GameObject Brush ile prefab'lar (sandık, fıçı, meşale, sütun, kayalar, kalıntılar, ...) |

## 3. Hangi Tilemap'e ne boyanır

`Level_01 > Grid` altında:

| Tilemap | Sıra (Order) | Collider | Ne boyanır |
|---|---|---|---|
| `Ground` | -100 | yok | Sadece zemin. |
| `Decor` | -95 | yok | Zemin dekoru (çatlak, slime, ot). Zemin üstüne. |
| `Walls` | -90 (oyuncunun arkası) | **var** (Composite, Wall katmanı) | Duvarlar ve engel olması gereken her şey. |
| `WallTop` | 20 (oyuncunun önü) | yok | Oyuncunun önünden geçmesi gereken parçalar (duvar üst kenarı/çıkıntılar). Walls paletindeki tile'larla boyanır. |
| `Props` | prefab'a bağlı | prefab'a bağlı | Sadece `Dungeon_Props` (GameObject Brush). |
| `Layout_Guide` | 30 | yok | **Boyama.** Oda yerleşim rehberi, oyunda gizlenir (`LayoutGuide`). |

Yanlış yer: zemini `Walls`'a boyama (oyuncu geçemez), duvarı `Ground`'a boyama (collider yok, oyuncu içinden geçer), Decor/Props'u `Walls`'a boyama.

**Layout_Guide**: yarı saydam mavi = oda, sarı = zemin kenarı/koridor, kırmızı = boss odası, turuncu = kapı. Oda sınırlarına, kapılara bakarak boyayıp işin bitince rehberi Hierarchy'de gizle (göz ikonu) ya da sil. Room, Door, düşman ve kamera sınırı nesneleri `Level` altında yerinde duruyor.

## 4. Hızlı boyama: otomatik duvar ve zemin

* **Duvar**: `Dungeon_Walls` > en üst satırdaki `Wall_Auto` tile'ını seç, `Walls` Tilemap'ine boya. Yalnızca "duvar bölgesini" boyaman yeterli, tile komşularına bakıp kendi parçasını seçer:
  * Üstünde duvar yoksa taş **kapak** (üst yüz), solu/sağı boşsa yuvarlak/pürüzlü **kenar**, tekil 1 hücre genişlik **sütun**.
  * Altında duvar yoksa kaya yüzünün **alt kenarı**, aradaki her şey kaya **dolgu**.
* **Kalın duvar**: kapak + dolgu satırlarını Box Fill (U) ile 4-5 hücre kalınlıkta boya; dışarıdaki siyah boşluğu da aynı tile ile doldur (dış dolgu). Kapak hep dolgu bloğunun **üst** satırında, oda zemininin altında kalan yüz ise zeminle birleşen satırdır. Pakette duvarlar odanın yukarısında 3-4 satır kalınlıktadır; Sample_Room'daki ölçü (üst 4, yanlar 3, alt 3) iyi bir başlangıç.
* **Zemin**: `Floor_Auto` tile'ı `Ground`'a boya. Pozisyona göre 16 varyasyonu dikişsiz döşer; üstünde zemin olmayan hücre (duvar yüzü altı) otomatik gölgelenir.
* Boss odası için `Wall_Auto_Boss` / `Floor_Auto_Boss`.
* Elle seçmek istersen paletin alt kısmındaki ham parçaları kullan (her sayfa PNG'deki 4x4 düzeniyle duruyor). `Wall_Auto` bir *custom tile*'dır (`DungeonWallTile`, Rule Tile paketi gerekmeden), yalnızca 4 yön komşuya bakar; dar iç köşelerde ya da farklı bir stil istersen ham parçayı elle üstüne boya.
* İpucu: Hata yapınca `Ctrl+Z`; yanlış bölgeyi Erase (D) ya da Box Erase (Shift+U) ile sil, komşu duvarlar kendini günceller.

## 5. Kapı kemeri

Paletteki kemerli kapı (`WL_10`/`WL_11`) 4x4: sol yarı 2 sütun + sağ yarı 2 sütun. Duvar kalınlığı 4 satır olmalı (kapak + 3 yüz); paletteki bloğu seçip (Select aracıyla sürükle), kapı yerine damgala. Kapı tile'larında **tile collider'ı yoktur** (süs kapı, güvenilir bloklamaz). Geçilmez bir kapı gerekiyorsa kemerin altına `Walls` altında boş bir GameObject + BoxCollider2D (Wall katmanı) koy (Level_01'de `Walls/ArchBlockers`); oyunda odayı kapatan gerçek kapılar `Level` altındaki `Door_*` nesneleridir (kendi collider'ı var, oda temizlenince açılır). Gerçek kapıyı kemerin içinde konumlandır.

Kapı geçişi (koridor) kalınlığı 2 hücre; duvarı Erase edip aralığı bırak.

## 6. Collider otomatik güncellenir

`Walls` üzerindeki `TilemapCollider2D` + `CompositeCollider2D` boyadıkça kendini günceller; sahne açılışında boş kalma sorununa karşı `TilemapColliderRefresh` bileşeni vardır. Oyuncu ve düşmanlar (Wall katmanı) duvara girmez. Kontrol: `Walls`'ı seç, Scene'de yeşil collider çizgisine bak. (Play testini sen yap.)

`Walls`'a collider'sız süs istersen `WallTop` ya da `Decor` kullan.

## 7. Props

`Props` Tilemap'ini aktif yap, `Dungeon_Props`'ta **GameObject Brush** seçili olsun (palette penceresindeki Brush açılır listesi). Prefab'ı seç, sahnede tıkla; nesne `Props` altına çocuk olarak girer. Sandık/fıçı/kaya gibi katı olanların ayak collider'ı vardır; meşale/afiş duvar süsüdür (duvar önü sırası). Sıralama proje Y-sort'una göredir: pivotu (ayakları) belirler. Küçük kaydırma için nesneyi taşıyabilirsin.

## 8. Sample_Room

Level'ın doğusunda (x 41-56, y -1..12) küçük bir örnek oda boyalı: kalın duvarlar, kemerli kapı, meşaleler, çatlak/slime, sandık, fıçı, sütun. Başlangıç referansı; işin bitince `Walls/Ground/Decor/Props` içinden bu bölgeyi sil. Oyuncu bölgesi dışında olduğu için kamera/oda sınırlarını etkilemez.

## 9. Yeniden üretme

* `Tools > Level > Build Dungeon Palettes`: tile asset'lerini, palette'leri ve yeni prop prefab'larını (paketten) yeniden kurar; paketi yeniden import edersen çalıştır.
* `Tools > Level > Setup Level_01 For Painting`: Level_01'i boyanabilir hale getirir (zaten yapıldı; yeniden çalıştırmak Ground/Walls'ı temizler!). Eski otomatik düzen için `Level_01_Auto.unity`.

## 10. Boyanmış Level_01 ve `Tools/Level/Paint Level_01`

Level_01 koddan boyandı (`LevelPainter.cs`), paketin tanıtım görselindeki dizilişle. Zemin olmayan her hücre kayadır ve parçasını kendisi seçer:

* **Yüz**: altında 1-4 hücre içinde zemin olan kaya. `wall_1` sayfasının 4 satırı (üstte taş kapak, kaya, zemine oturan pürüzlü alt kenar); alçak duvarda aynı sayfanın daha az satırı. Paletteki `WL_1_*`.
* **Sütun**: yanında zemin ya da yüz olan (veya köşeden zemine değen) kaya. 1 hücre genişlikte taş blok sütunu (`WL_3_*`); oda yanları, koridor kenarları, duvar uçları.
* **Kapak şeridi**: hemen üstünde zemin olan kaya (yukarıdaki odadan görülen duvar): `WL_4_*`.
* **Kaya üstü**: geri kalan her şey; koyu kaya (`BD_*`). Bu da `Walls`'ta ve collider'lı (dışarısı tek parça). 1-2 hücrelik ince boşluklar sütun / kapak taşıyla doldurulur.
* Yüzün dibindeki zemin satırı gölgelidir (`CP_shadow_*`). Boss bölgesi kırmızı tonludur (`*_boss`).

Odalar: alkovlar, yan duvardan çıkan 3 satırlık duvar parçaları (serbest ucunda sütun), alt duvardan yükselen 1 hücrelik sütunlar. Ayrıca üç kemerli kapı süsü (`Walls/ArchBlockers` kapatır), yüz diplerinde meşale ve mercan otu, köşelerde sandık/fıçı/kasa grupları, sütun prop'ları, boss odasında altın yığınları ve sancaklar var.

Menü komutu `Ground/Decor/Walls/WallTop/Backdrop/Props` içeriğini **siler** ve baştan boyar; elle değişiklik yaptıktan sonra çalıştırma. Oda/alkov/çıkıntı yerleşimi ve prop grupları `LevelPainter.cs` başındaki tablolarda. `Tools/Level/Render Level_01 Shots` Edit modunda `Assets/Screenshots/lvl_*.png` (oda başına geniş plan) ve `game_*.png` (oyun kamerası boyutu) render'larını üretir.

Elle aynı görünümü boyamak için: oda yanına `WL_3` sütunu, oda üstüne 4 satır `WL_1` (kapak en üstte), oda altına `WL_4` kapak şeridi, dışarıya `BD_*` koy. `Wall_Auto` bu ayrımı yapamaz (zemini görmez), hızlı taslak için kalır.
