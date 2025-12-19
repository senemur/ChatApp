# ChatApp - Real-time Chat Application

ASP.NET Core MVC, SignalR ve SQL Server kullanarak geliştirdiğim, çok kullanılan bir uygulamanın benzeri özelliklere sahip modern ve gerçek zamanlı bir sohbet uygulaması. Clean Architecture prensiplerine uygun olarak tasarlanmıştır.

## 🚀 Özellikler

### Temel Özellikler
- **Gerçek Zamanlı Mesajlaşma**: SignalR altyapısı ile anlık iletilen mesajlar.
- **Bire-bir ve Grup Sohbetleri**: İster özel ister grup içinde sohbete başlayın.
- **Kullanıcı Durumları**: Online/Offline takibi ve "Son görülme" zamanı.
- **Yazıyor Göstergesi**: Karşı tarafın mesaj yazdığını anlık olarak görün.
- **Mesaj Durumları**:
  - ✓ Gönderildi
  - ✓✓ Teslim Edildi (Okundu varsayımı ile)
  - 👁️ Okundu Bilgisi (Mavi tik benzeri)

### Medya ve Zengin İçerik
- **🎤 Sesli Mesaj (Voice Notes)**: Tarayıcı üzerinden ses kaydı yapıp gönderme.
- **📷 Resim Paylaşımı**: Sohbet içinde görsel dosyaları paylaşma ve görüntüleme.
- **😊 Emoji Desteği**: Entegre emoji seçici ile duygularınızı ifade edin.

### Mesaj Yönetimi
- **Düzenleme**: Gönderilen mesajları düzenleyebilme.
- **Silme**: Mesajları hem kendinizden hem karşı taraftan silebilme.

## 📸 Ekran Görüntüleri

Uygulamadan kareler:

| Sohbet Ekranı | Sesli Mesaj & Emoji |
|:---:|:---:|
| ![4](https://github.com/user-attachments/assets/1ec70121-f124-4afd-8a75-79cd81f4662b) | ![5](https://github.com/user-attachments/assets/2ce7f559-bd6c-472b-becf-f2aa7e030e28)|
| ![chatapp1](https://github.com/user-attachments/assets/7738bc8d-84c5-4bf6-a274-e08e3d3e6fb7) | ![caht2](https://github.com/user-attachments/assets/a71abe49-36ec-4191-bceb-1863723c73bb)
| ![3](https://github.com/user-attachments/assets/8547c424-bbce-4270-b3b5-becbe8181255)|


## 🛠️ Teknolojiler

- **Backend**:
  - ASP.NET Core 8.0 MVC
  - SignalR (WebSocket)
  - Entity Framework Core
  - ASP.NET Identity (Auth)
  - SQL Server
  
- **Frontend**:
  - Razor Views
  - Vanilla JavaScript (ES6+)
  - **Emoji Picker Element** (Web Component)
  - Bootstrap 5
  - CSS3 (Animations & Responsive)

## 🏗️ Mimari

Proje, sürdürülebilirlik ve test edilebilirlik için **Clean Architecture** prensiplerine göre katmanlara ayrılmıştır:

1. **ChatApp.Domain**: Entity'ler (User, Message, Conversation) ve temel arayüzler. Dış bağımlılığı yoktur.
2. **ChatApp.Application**: İş mantığı, servis tanımları (IFileUploadService) ve DTO'lar.
3. **ChatApp.Infrastructure**: Veritabanı (DbContext), veri erişimi ve dış servis implementasyonları.
4. **ChatApp.Web**: Kullanıcı arayüzü, Controller'lar ve SignalR Hub (ChatHub).

## 🚀 Kurulum

1. Repoyu klonlayın.
2. `appsettings.json` içindeki Connection String'i kendi SQL Server'ınıza göre düzenleyin.
3. Package Manager Console üzerinde `Update-Database` komutunu çalıştırarak veritabanını oluşturun.
4. Projeyi çalıştırın (`ChatApp.Web`).

---

