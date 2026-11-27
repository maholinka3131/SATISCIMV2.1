# Satis Takip

Windows icin C# / WPF tabanli SATIŞÇIM satis takip ve kasa uygulamasi.

## Kalici veri

SQLite veritabani su konumda tutulur:

```text
C:\Users\Kullanici\AppData\Local\SatisTakip\satis_takip.db
```

Uygulama acilisinda varsayilan yonetici girisi `admin / 051575045` bilgisidir.

Uygulamada kalici olarak urun/stok, satis, iade, gider, musteri/veresiye, tahsilat, kullanici ve stok hareketi kayitlari bulunur. Sol menudeki veritabani yedekleme ve geri yukleme secenekleri kullanilabilir.

Urun kategorileri kalici olarak saklanir. Kasa ekraninda kategori dugmeleriyle urunler filtrelenebilir; kategori arama alanindan kategori bulunabilir. Yeni urun eklerken mevcut kategori secilebilir veya yeni kategori eklenebilir.

Kategori dugmesine sag tiklayarak adini duzenleyebilir veya silebilirsiniz. Silmeden once kategorideki urunler listelenir; onaylaninca urunler korunarak `Genel` kategorisine tasinir. Ana ekrandaki ve Urunler penceresindeki urunlere sag tiklayarak urun kategorisi degistirilebilir. `Genel` varsayilan kategori oldugundan silinemez veya yeniden adlandirilamaz.

Katalogdan silinen bir urunun barkodu yeniden kullanilabilir. Yeni kayit ayni urun kaydini yeniden etkinlestirir; gecmis satis ve stok hareketi baglantilari korunur.

Yonetici ayarlarindaki fabrika ayarina donme islemi urun, stok hareketi, kategori, satis, musteri, gider ve uygulama ayarlarini siler; yonetici hesabini koruyup diger kullanici hesaplarini kaldirir.

Urunlerde kritik stok esigi ayarlanabilir (varsayilan 5); bu esik ve altindaki urunler `Kritik stok` ekraninda listelenir. Stok girisleri, satislar, iadeler ve elle yapilan stok duzeltmeleri hareket gecmisine kullanici bilgisiyle kaydedilir.

`Satış Raporları` ekraninda tarih araligina gore net satis, maliyet, brut kar ve adet bazinda en cok satilan urunler goruntulenir. Karma odeme tutarlari nakit ve kart olarak ayri saklanir, satis gecmisi ve gun sonu raporunda da bu paylar ayri gosterilir. Yonetici `Kullanici yonetimi` ekranindan yonetici ve kasiyer hesaplari olusturabilir. Kasiyer kasa, musteri/veresiye ve yalnizca goruntulenebilir kritik stok uyarilarina erisebilir.

## Kurulum ve guncelleme

Kurulum dosyasi [GitHub Releases](https://github.com/maholinka3131/SATISCIMV2.1/releases/latest) sayfasindan indirilebilir. Uygulama Windows x64 icin self-contained yayinlanir; kullanicinin bilgisayarinda ayrica .NET Runtime kurulu olmasi gerekmez. Yeni setup ayni `AppId` ile derlendigi icin eski uygulamanin uzerine guncelleme yapar; kullanici veritabani `AppData\Local\SatisTakip` altinda kaldigi icin silinmez.

Uygulama acilisinda GitHub'daki `update.json` dosyasini kontrol eder. Daha yeni bir surum varsa indirme baglantisinin acilmasi icin kullanicidan onay ister; indirilen setup calistirilarak guncelleme tamamlanir.

## Calistirma

```powershell
cd SatisTakip
dotnet run
```

## Excel aktarim formati

Ilk sayfanin ilk satiri baslik olarak atlanir. Sutunlar su sirada olmali:

1. Barkod
2. Urun adi
3. Kategori
4. Satis fiyati
5. Alis maliyeti
6. Stok miktari (istege bagli)
7. Kritik stok esigi (istege bagli, varsayilan 5)
8. Son kullanma tarihi (istege bagli; ilk stok girisinde zorunlu)

Ornek:

| Barkod | Urun adi | Kategori | Satis fiyati | Alis maliyeti | Stok | Kritik stok esigi | Son kullanma tarihi |
|---|---|---|---:|---:|---:|---:|---|
| 1001 | Tam Yagli Sut | Sut Urunleri | 32,50 | 24,00 | 12 | 5 | 31.12.2026 |

Excel aktarimindaki `Alis maliyeti` dogrudan kaydedilir ve mevcut formatta KDV orani aktarilmaz; KDV oranini sonradan urun duzenleme ekranindan tanimlayabilirsiniz.

Urun ekleme ve duzenleme ekraninda alis maliyeti KDV haric girilir. `%1`, `%10` veya `%20` KDV orani secildiginde KDV dahil alis maliyeti otomatik hesaplanir ve bu tutar urunun maliyeti olarak kullanilir. Barkodla stok girisinde maliyet alani bos birakilirsa mevcut maliyet korunur; yeni maliyet girilirse urunun kayitli KDV oraniyla hesaplanir. Eski urunlerde KDV orani kayitli olmadigindan, ilk maliyet guncellemesinde oran secilmelidir. Satis fiyati bu hesaplamayla otomatik degismez.

Ilk stok girisinde SKT zorunludur. Mevcut urunlerin SKT ve adet bilgileri `SKT / adet tanimla` ekranindan, SKT tanimlanmamis stok miktari kadar eklenebilir. Satislar en yakin SKT'li partiden dusulur; uygulama acilisinda yalnizca SKT'sine 5 gun veya daha az kalan ve hala stokta olan partiler uyarilir.

## Release cikti

```powershell
dotnet publish SatisTakip\SatisTakip.csproj -c Release -r win-x64 --self-contained true -o publish
```

Gun sonu ekraninda toplam ciro, urun maliyeti, brut kar, toplam gider ve net kar gorulur. Rapor penceresindeki `Yazdir` dugmesiyle yaziciya aktarilabilir. Urun, satis ve stok hareketleri SQLite veritabaninda kalici olarak saklanir.
