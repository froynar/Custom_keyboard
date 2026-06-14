# Phase 8A SignalR Hotspot/VPN Demo Plan

Ngay lap tuc chua can trien khai. Tai lieu nay gom cac y tuong va thay doi can can nhac neu sau Phase 10 muon fork GitHub de thu nghiem chat realtime kieu Messenger trong pham vi demo LAN/hotspot/VPN.

## 1. Muc tieu

Nang chat hien tai tu:

```text
Chat DB-only + refresh thu cong
```

thanh:

```text
Chat luu DB + SignalR day su kien realtime khi hai ben online
```

Y nghia:

- Tin nhan van duoc luu ben vung vao SQL Server.
- SignalR chi dong vai tro "chuong bao realtime".
- Neu SignalR mat ket noi, tin nhan khong mat vi DB van la source of truth.
- Khi reconnect, app co the refresh lai messages tu DB.

Scope toi thieu:

- Buyer chat voi Seller realtime.
- Seller chat voi Buyer realtime.
- Admin chat voi Seller realtime.
- Seller chat voi Admin realtime.
- Khong lam Buyer-Admin direct chat.
- Khong lam typing indicator, unread badge, online presence, push notification khi app tat.

## 2. Mo hinh demo de xuat

### 2.1 May A lam demo server

May A dong vai tro server/operator demo:

```text
May A
- SQL Server
- SignalR ChatHost
- WPF app, co the dang nhap Admin de quan tri/demo
```

May B va C dong vai tro client:

```text
May B
- WPF app
- nhap IP may A
- dang nhap Buyer

May C
- WPF app
- nhap IP may A
- dang nhap Seller
```

Mo hinh:

```text
Phone hotspot / Radmin VPN / NordVPN Meshnet
        |
        +-- Machine A
        |     - SQL Server :1433
        |     - SignalR ChatHost :5000
        |     - WPF app Admin/operator
        |
        +-- Machine B
        |     - WPF app Buyer
        |     - connects to A:1433
        |     - connects to A:5000/chatHub
        |
        +-- Machine C
              - WPF app Seller
              - connects to A:1433
              - connects to A:5000/chatHub
```

### 2.2 Khong tao role ServerOperator

Khong nen them role moi vao bang `roles`.

Giu role nghiep vu:

```text
Buyer
Seller
Admin
```

Ly do:

- `Server operator` la vai tro van hanh ha tang, khong phai nghiep vu build keyboard.
- Neu DB chua ket noi duoc thi khong the dang nhap role trong DB de cau hinh DB.
- Them role moi se lam ERD/use case/test/docs phinh khong can thiet.
- Admin co the la nguoi van hanh demo, nhung admin trong app van chi nen quan ly user/seller/catalog/audit.

Neu can UI ho tro demo, co the lam:

```text
Demo Server Panel
```

Panel nay co the chi hien cho Admin hoac chi hien khi bat config `EnableDemoServerPanel = true`.

## 3. SignalR la gi trong du an nay

SignalR la mot realtime hub/server nho. No mo endpoint dang:

```text
http://<server-ip>:<port>/chatHub
```

Vi du:

```text
http://192.168.43.25:5000/chatHub
```

Trong do:

- `192.168.43.25` la IP may A trong hotspot/LAN/VPN.
- `5000` la port SignalR host.
- `/chatHub` la route cua hub chat.

SignalR khong thay SQL Server.

Luon dung nguyen tac:

```text
Save message to DB first
Then publish realtime event
```

Luồng:

```text
Buyer/Seller/Admin gui tin
        |
        v
ChatService kiem tra participant va rule buyer-seller/admin-seller
        |
        v
SqlChatRepository luu vao chat_messages
        |
        v
SignalR publish MessageCreated event theo conversation
        |
        v
Client dang online nhan event va append/refresh message
```

### 3.1 Realtime layer hien co sau Phase 10

Sau Phase 10, project khong con la greenfield realtime nua. Repo da co lop:

```text
Realtime/IRealtimeNotifier.cs
Realtime/IRealtimeSubscriber.cs
Realtime/MqttRealtimeService.cs
Realtime/MqttSettings.cs
```

Lop nay dang dung MQTT cho realtime notification cua build request/status:

```text
RequestService save DB
        |
        v
IRealtimeNotifier publish request/status event best-effort
        |
        v
IRealtimeSubscriber nhan event va dashboard reload tu DB
```

Vi vay truoc khi implement chat realtime can quyet dinh ro:

```text
Dung tiep MQTT hien co cho MessageCreated
hoac
Dung SignalR rieng cho chat va ghi ro ly do
```

Khong nen xem Phase 8A nhu mot stack realtime hoan toan moi ma khong nhac den MQTT da co.

### 3.2 Lua chon A - Tai dung MQTT hien co

Huong nay hop neu muc tieu la demo nhanh va giam so process phai chay.

Can mo rong interface hien co:

```text
IRealtimeNotifier.MessageCreatedAsync(conversationId, messageId)
IRealtimeSubscriber.ChatMessageCreated
```

Topic goi y:

```text
keyboard/chat/conversation/{conversationId}/message-created
keyboard/chat/user/{userId}/message-created
```

Nen uu tien `user/{userId}` hoac subscribe tat ca conversation cua user khi login neu muon conversation list cung cap nhat realtime. Neu chi subscribe conversation dang mo thi demo don gian hon, nhung conversation list va hoi thoai chua mo se khong realtime.

Uu diem:

- Khong can tao `CustomKeyboard.ChatHost`.
- Khong can them process `dotnet run` thu hai.
- Dung tiep broker MQTT da test cho LAN/hotspot/VPN.
- Giu cung pattern DB-first, publish best-effort, receiver reload tu DB.

Nhuoc diem:

- MQTT la pub/sub ha tang, khong giong "Messenger web socket" bang SignalR.
- Can thiet ke topic/user channel can than de tranh leak event rong.
- Broker Mosquitto van la dependency ha tang tren may A.

### 3.3 Lua chon B - Van dung SignalR cho chat

Huong nay hop neu muc tieu la thu nghiem chat kieu Messenger sat voi app realtime .NET hon:

- SignalR co group theo conversation/user rat tu nhien.
- WPF client co `HubConnection` va auto reconnect ro rang.
- Chat co the tach khoi MQTT request/status de demo hai cong nghe khac nhau.

Neu chon huong nay, tai lieu phai ghi ro:

- Phase 8 MQTT request/status khong bi thay the.
- SignalR la realtime stack thu hai, co them chi phi van hanh `ChatHost`.
- Interface nen dat sau abstraction chung de tranh trung lap vo nghia voi `IRealtimeNotifier/IRealtimeSubscriber`.

Ten de xuat neu giu SignalR:

```text
IChatRealtimeNotifier
IChatRealtimeSubscriber
SignalRChatRealtimeService
ChatRealtimeSettings
```

Hoac neu muon gom lai:

```text
IRealtimeNotifier them MessageCreatedAsync
IRealtimeSubscriber them ChatMessageCreated
MqttRealtimeService va SignalRChatRealtimeService la 2 implementation khac nhau cho chat event
```

### 3.4 Khuyen nghi cho fork demo

De giam rui ro sau Phase 10, thu tu quyet dinh nen la:

1. Neu chi can chat realtime LAN/hotspot nhanh: mo rong MQTT hien co cho `MessageCreated`.
2. Neu muon hoc/demo SignalR rieng: giu plan SignalR, nhung ghi ro day la stack song song co chu dich.
3. Trong ca hai huong, event realtime chi nen mang id:

```text
MessageCreated(conversationId, messageId)
```

Client nhan event roi goi `ChatService.GetMessagesAsync(...)` de refresh tu DB. Cach nay vua don gian vua an toan hon append payload, vi `ChatService.GetMessagesAsync` van kiem tra participant truoc khi tra noi dung.

## 4. Cung hotspot/LAN thi chay nhu the nao

### 4.1 Setup mang

1. Dien thoai bat 4G/5G.
2. Bat Personal Hotspot/Mobile Hotspot.
3. May A, B, C cung ket noi vao hotspot do.
4. Tren may A lay IP:

```powershell
ipconfig
```

Tim adapter Wi-Fi va lay `IPv4 Address`, vi du:

```text
192.168.43.25
```

May B/C se nhap IP nay.

### 4.2 May A can chay gi

May A:

```text
1. Chay SQL Server va database cua du an.
2. Chay SignalR ChatHost tren port 5000.
3. Chay WPF app neu can dang nhap Admin/Buyer/Seller tai may A.
4. Mo firewall cho port 5000 va 1433.
```

Vi du lenh chay host:

```powershell
dotnet run --project CustomKeyboard.ChatHost
```

Host nen listen:

```text
http://0.0.0.0:5000
```

Khong nen chi listen:

```text
http://localhost:5000
```

vi may B/C se khong truy cap duoc neu host chi bind localhost.

### 4.3 May B/C can lam gi

May B/C:

```text
1. Chay WPF app.
2. Chon Connect to demo server.
3. Nhap IP may A.
4. Bam Test Connection.
5. Dang nhap Buyer/Seller.
6. Mo chat va nhan tin realtime.
```

Neu may A IP la:

```text
192.168.43.25
```

App tu ghep:

```text
SQL Server = 192.168.43.25,1433
SignalR Hub = http://192.168.43.25:5000/chatHub
```

## 5. Radmin VPN / NordVPN Meshnet

Hotspot/LAN khong phai cach duy nhat. Co the dung LAN ao.

### 5.1 Radmin VPN

Dung duoc va kha hop cho demo, giong Minecraft LAN.

Dieu kien:

- A, B, C cung join mot Radmin network.
- B/C ping duoc IP Radmin cua A.
- May A mo firewall port 5000 va 1433.
- SignalR host listen `0.0.0.0:5000`.
- SQL Server tren A bat TCP/IP remote.

Neu Radmin cap cho may A IP:

```text
26.41.12.8
```

B/C nhap:

```text
26.41.12.8
```

App tu ghep:

```text
SignalR = http://26.41.12.8:5000/chatHub
SQL = 26.41.12.8,1433
```

### 5.2 NordVPN

Chi dung duoc neu la NordVPN Meshnet hoac tinh nang LAN ao tuong tu.

VPN doi region thong thuong khong du.

Dieu kien:

- A, B, C bat Meshnet.
- Cho phep traffic giua cac thiet bi.
- B/C ket noi den IP/tên Meshnet cua A.
- May A van mo firewall/port nhu tren.

## 6. Firewall la gi va vi sao can mo

Firewall Windows co the chan may khac ket noi vao may A.

May B/C muon ket noi vao may A qua:

```text
SignalR: TCP 5000
MQTT neu chon MQTT-first: TCP 1883
SQL Server: TCP 1433
```

Nen may A can cho phep inbound traffic tren cac port nay.

Lenh mo firewall tren may A, PowerShell Admin:

```powershell
New-NetFirewallRule -DisplayName "Keyboard SignalR 5000" -Direction Inbound -Protocol TCP -LocalPort 5000 -Action Allow
New-NetFirewallRule -DisplayName "Keyboard MQTT 1883" -Direction Inbound -Protocol TCP -LocalPort 1883 -Action Allow
New-NetFirewallRule -DisplayName "Keyboard SQL Server 1433" -Direction Inbound -Protocol TCP -LocalPort 1433 -Action Allow
```

Test tu may B/C:

```powershell
Test-NetConnection 192.168.43.25 -Port 5000
Test-NetConnection 192.168.43.25 -Port 1883
Test-NetConnection 192.168.43.25 -Port 1433
```

Neu thay:

```text
TcpTestSucceeded : True
```

la port da thay duoc.

## 7. SQL Server remote

Voi kien truc hien tai, WPF app dang ket noi SQL truc tiep qua repository. Neu B/C dung chung database may A thi B/C phai ket noi duoc SQL Server cua A.

Can:

- Bat TCP/IP trong SQL Server Configuration Manager.
- Co dinh SQL port 1433.
- Restart SQL Server service.
- Mo firewall port 1433.
- Nen dung SQL login/password thay vi `Integrated Security=True`.

Vi du connection string cho B/C:

```text
Data Source=192.168.43.25,1433;Initial Catalog=CustomKeyboardBuilder;User ID=keyboard_demo;Password=Password123;Encrypt=True;TrustServerCertificate=True
```

Rui ro lon nhat cua demo nhieu may thuong nam o SQL remote, khong phai SignalR.

Huong kien truc sach hon ve sau:

```text
WPF client -> API/SignalR server -> SQL Server
```

Khi do B/C khong can mo SQL port 1433. Tuy nhien huong nay can refactor lon hon, khong nen chen vao neu muc tieu chi la Phase 8A demo.

## 8. Repo GitHub va cach chay

Khong nen tach hai ban code A/B.

Nen co mot repo duy nhat:

```text
Custom_keyboard/
├─ Custom_keyboard.csproj
├─ CustomKeyboard.ChatHost/
│  ├─ CustomKeyboard.ChatHost.csproj
│  ├─ Program.cs
│  └─ Hubs/
│     └─ ChatHub.cs
├─ Realtime/
│  ├─ SignalRChatClient.cs
│  └─ ChatRealtimeSettings.cs
├─ Database/
├─ Documents_Refactor/
└─ README.md
```

May A clone repo va chay:

```powershell
dotnet run --project CustomKeyboard.ChatHost
dotnet run --project Custom_keyboard.csproj
```

May B/C clone repo va chi chay:

```powershell
dotnet run --project Custom_keyboard.csproj
```

B/C van co source ChatHost trong repo, nhung khong can chay.

Khac nhau khong nam o file nao duoc tai ve, ma nam o:

```text
May A chay them SQL + ChatHost
May B/C chi chay WPF app va nhap IP may A
```

## 9. UI/config de xuat

Nen them man hinh truoc login:

```text
Connection Mode

( ) Local machine
( ) Connect to demo server

Server IP: [              ]

[Test Connection] [Continue]
```

Neu chon Local:

```text
SQL = localhost / current configured SQL Server
SignalR = http://localhost:5000/chatHub
```

Neu chon Connect to demo server va nhap `192.168.43.25`:

```text
SQL = 192.168.43.25,1433
SignalR = http://192.168.43.25:5000/chatHub
```

Luu y sau Phase 10:

```text
MainWindow.xaml.cs hien dang tao SqlConnectionFactory(),
repositories, services va MqttRealtimeService() ngay trong constructor.
```

Vi vay man hinh `Connection Mode` khong chi la them UI/settings. Can doi composition root:

```text
App start
        |
        v
ConnectionSettingsViewModel nhan mode/IP
        |
        v
Build services voi SqlServerSettings + MqttSettings/ChatRealtimeSettings da chon
        |
        v
Show Login
```

Diem tiem da co:

- `SqlConnectionFactory(SqlServerSettings?)`
- `MqttRealtimeService(MqttSettings?)`

Nhung thoi diem khoi tao phai doi sang sau khi user chon Local/DemoServer.

Nen co nut `Test Connection`:

- Test SignalR port 5000/hub.
- Neu chon MQTT thay SignalR, test MQTT port 1883/broker.
- Test SQL connection.
- Bao loi ro:
  - Sai IP.
  - Firewall chan.
  - SQL Server chua bat TCP/IP.
  - ChatHost chua chay.
  - MQTT broker chua chay neu chon huong MQTT.
  - Sai username/password SQL.

Neu muon Admin tren may A co trai nghiem dep, co the them `Demo Server Panel`:

```text
Demo Server

Machine IP: 192.168.43.25
SignalR: http://192.168.43.25:5000/chatHub
SQL: 192.168.43.25,1433

[Copy IP]
[Copy client setup]
[Check ports]
```

Panel nay chi la ho tro demo, khong phai role nghiep vu moi.

## 10. Thay doi code du kien

Truoc khi code, chon mot trong hai huong:

```text
A. MQTT-first: mo rong realtime layer hien co cho chat MessageCreated
B. SignalR-first: them ChatHost rieng, nhung co ly do ro va khong tao abstraction trung lap khong can thiet
```

### 10.0 Neu chon MQTT-first

Khong them project `CustomKeyboard.ChatHost`.

Sua/mo rong:

```text
Realtime/IRealtimeNotifier.cs
Realtime/IRealtimeSubscriber.cs
Realtime/MqttRealtimeService.cs
Realtime/MqttSettings.cs
ViewModels/MainShellViewModel.cs
ViewModels/ChatViewModel.cs
Services/ChatService.cs hoac Chat use-case wrapper
```

Event toi thieu:

```text
MessageCreated(conversationId, messageId)
```

Flow de xuat:

```text
ChatService.SendMessageAsync -> save DB
WPF/use-case publish MessageCreated best-effort
Receiver nhan event -> neu lien quan user/conversation -> refresh messages/conversation list tu DB
```

Neu muon chat list realtime, dung per-user topic:

```text
keyboard/chat/user/{userId}/message-created
```

Neu chi can MVP don gian, co the subscribe topic cua conversation dang mo, nhung phai ghi ro conversation list va hoi thoai chua mo khong tu cap nhat.

### 10.1 Neu chon SignalR-first: them project SignalR host

Them project:

```text
CustomKeyboard.ChatHost
```

Chua:

- `Program.cs`
- `Hubs/ChatHub.cs`
- cau hinh listen `0.0.0.0:5000`

Hub co the co:

```text
JoinConversation(conversationId)
LeaveConversation(conversationId)
SendMessageCreated(conversationId, messageId)
```

Neu can conversation list realtime hoac tin nhan den khi hoi thoai chua mo, them group theo user:

```text
JoinUser(userId)
MessageCreatedForUser(userId, conversationId, messageId)
```

### 10.2 Neu chon SignalR-first: them realtime client trong WPF

Them:

```text
Realtime/IChatRealtimeNotifier.cs
Realtime/IChatRealtimeSubscriber.cs
Realtime/SignalRChatRealtimeService.cs
Realtime/ChatRealtimeSettings.cs
```

Client can:

- connect sau login
- disconnect khi logout/app close
- join conversation khi mo chat
- nhan event `MessageCreated`
- goi callback de ChatViewModel append/refresh
- auto reconnect

Khong nen tao `IChatRealtimeClient` nhu mot abstraction song song mo ho voi `IRealtimeNotifier/IRealtimeSubscriber` da co, tru khi co ly do ro. Ten notifier/subscriber giup giu cung model publish/subscribe voi MQTT Phase 8.

### 10.3 Sua ChatViewModel

Them:

- connect realtime sau khi user vao dashboard
- subscribe `MessageCreated`
- neu event thuoc conversation dang mo thi refresh messages hoac append message
- tranh duplicate message khi vua send xong vua nhan event

Huong an toan:

```text
Nhan event -> refresh messages tu DB
```

Huong muot hon:

```text
Nhan event -> fetch message by id hoac append payload
```

MVP nen chon refresh messages tu DB de tranh sai dong bo.

Ly do bao mat:

- Hub/broker demo LAN co the chua auth chat user day du.
- Neu event chi co `conversationId, messageId`, client van phai goi `ChatService.GetMessagesAsync`.
- `ChatService.GetMessagesAsync` kiem tra participant, nen nguoi ngoai khong doc duoc noi dung chat du chi nghe thay event.
- Neu append payload co noi dung tin nhan qua hub/broker khong auth, rui ro leak noi dung cao hon.

### 10.4 Flow gui tin

Nen giu:

```text
ChatService.SendMessageAsync -> save DB
```

Sau do publish realtime event:

```text
MessageCreated(conversationId, messageId)
```

Co 2 cach:

1. WPF client publish event sau khi `SendMessageAsync` thanh cong.
2. Mot service/use-case chung save DB roi publish SignalR.

Cho demo nhanh, cach 1 de lam hon.

Cho kien truc sach hon, cach 2 tot hon.

Can lam ro: cach 2 chi that su sach neu tao mot use-case/application service o WPF layer, hoac refactor lon sang:

```text
WPF client -> API/SignalR server -> SQL Server
```

Neu hieu cach 2 la "server nhan lenh gui tin roi tu save DB", thi no chinh la refactor API/server o muc 7, khong phai viec nho rieng biet.

### 10.5 Config connection

Them settings runtime:

```text
ConnectionMode = Local | DemoServer
ServerIp
SqlHost
SqlPort = 1433
RealtimeMode = MQTT | SignalR
MqttPort = 1883
SignalRPort = 5000 neu chon SignalR
HubPath = /chatHub neu chon SignalR
```

Khong hard-code IP vao source.

IP la runtime setting do user nhap.

## 11. Thay doi diagram/tai lieu can thuc hien neu implement

### 11.1 Khong can sua

- ERD/DBML: khong doi vi SignalR khong them bang.
- Build validation logic: khong lien quan.
- Build/request core DFD: chi can sua neu muon the hien notification request, nhung Phase 8 MQTT da co rieng.

### 11.2 Nen sua

#### DFD Context / Level 0

Them hoac doi:

```text
5.0 Quan ly chat & realtime delivery
```

Hoac them tien trinh:

```text
6.0 Realtime Hub
```

Neu chon MQTT-first, ten nen trung tinh hon:

```text
6.0 Realtime Broker
```

Flow:

```text
P5 Quan ly chat -> D7 Chat: luu message
P5 Quan ly chat -> P6 Realtime Hub/Broker: MessageCreated event
P6 Realtime Hub/Broker -> Buyer/Seller/Admin: realtime message notification
```

#### DFD Level 1

Trong `5.0 Quan Ly Chat`, them:

```text
5.5 Phat su kien realtime
```

Flow:

```text
5.3 Luu tin nhan -> D7 Chat
5.3 Luu tin nhan -> 5.5 Phat su kien realtime
5.5 -> Buyer/Seller/Admin dang online
5.4 Tra cuu lich su chat dung khi mo chat/reconnect
```

Neu chi subscribe conversation dang mo, ghi ro:

```text
5.5 chi cap nhat realtime cho conversation dang duoc join.
Conversation list va hoi thoai chua mo can refresh thu cong hoac per-user channel.
```

#### DFD Level 2

Doi `5.3 Luu Tin Nhan` thanh:

```text
5.3.1 Nhan tin nhan
5.3.2 Kiem tra conversation
5.3.3 Luu tin nhan vao DB
5.3.4 Publish MessageCreated event
5.3.5 Client nhan event va refresh/append
```

Can lam ro:

```text
DB = luu ben vung
SignalR = day su kien realtime
```

#### Class Diagram

Neu chon MQTT-first, them/mo rong:

```text
IRealtimeNotifier.MessageCreatedAsync()
IRealtimeSubscriber.ChatMessageCreated
MqttRealtimeService
ChatViewModel --> IRealtimeSubscriber
Chat send use-case --> IRealtimeNotifier
```

Neu chon SignalR-first, them:

```text
ChatHub
IChatRealtimeNotifier
IChatRealtimeSubscriber
SignalRChatRealtimeService
ChatRealtimeSettings
```

Quan he:

```text
ChatViewModel --> IChatRealtimeSubscriber
IChatRealtimeNotifier <|.. SignalRChatRealtimeService
IChatRealtimeSubscriber <|.. SignalRChatRealtimeService
SignalRChatRealtimeService --> ChatHub
ChatService --> IChatRepository
```

Neu dung notifier trong service:

```text
ChatService --> IChatRealtimeNotifier
IChatRealtimeNotifier <|.. SignalRChatRealtimeService
```

#### MVVM Diagram

Them vao `ChatViewModel`:

```text
ConnectRealtimeAsync()
DisconnectRealtimeAsync()
OnMessageReceived()
```

Neu co man hinh nhap IP:

```text
ConnectionSettingsViewModel
ServerIp
TestConnectionCommand
```

#### FHD / Use Cases

Khong bat buoc ve lai lon.

Nen them mo ta:

```text
Chat luu DB va tu dong cap nhat realtime khi cac ben online.
Neu nguoi nhan offline/mat SignalR, tin nhan van xem duoc khi load lai tu DB.
```

Co the them function:

```text
5.5 Nhan tin nhan realtime khi online
```

#### Deployment/Network Diagram

Nen them file moi vi day la phan quan trong nhat cho demo.

Ten de xuat:

```text
Documents_Refactor/Custom_Keyboard_Deployment_Realtime_Demo.md
```

Hoac neu muon de ngoai:

```text
Realtime_Demo_Network_Diagram.md
```

Noi dung:

```text
Phone hotspot/Radmin/NordVPN Meshnet
May A: SQL Server + MQTT broker hoac ChatHost + WPF Admin
May B: WPF Buyer
May C: WPF Seller
Ports: 1433 SQL, 1883 MQTT neu chon MQTT, 5000 SignalR neu chon SignalR
B/C nhap IP A
```

## 12. Test/verification can them

Them vao `Phase6Verification` hoac runner rieng:

- Chat save DB first then publish realtime event.
- Publish fail khong lam mat message DB.
- Client receive event -> refresh messages.
- Duplicate event khong tao duplicate UI.
- Sender khong phai participant bi chan.
- Buyer-Admin direct chat van bi chan.
- Neu dung per-user channel, user nhan event cho conversation chua mo thi conversation list refresh.
- Neu chi dung conversation group, ghi ro test conversation chua mo khong realtime la accepted limitation.
- Neu hub/broker gui payload noi dung tin nhan, them test/ghi chu bao mat; MVP nen tranh huong nay.

Manual test:

```text
Single machine:
- Mo 2 instance WPF
- Buyer/Seller cung ket noi localhost
- MQTT: broker localhost:1883 hoac SignalR: ChatHost localhost:5000
- Gui tin va thay realtime

Hotspot:
- A server, B buyer, C seller
- B/C nhap IP A
- Gui tin hai chieu

Disconnect:
- Tat broker/ChatHost tam thoi
- Gui/refresh DB-only neu cho phep
- Bat lai broker/ChatHost va reconnect
```

## 13. Rủi ro va cach giam

### 13.1 IP doi

Khong hard-code IP.

Giai phap:

- B/C nhap IP runtime.
- A hien IP hien tai trong Demo Server Panel.
- Co nut Copy IP/Copy setup.

### 13.2 Firewall chan

Giai phap:

- README co lenh mo firewall.
- App co Test Connection.
- Bao loi ro port 1433/1883/5000 tuy realtime mode.

### 13.3 SQL remote kho cau hinh

Giai phap:

- Dung SQL login demo.
- Co dinh port 1433.
- Huong dan bat TCP/IP.
- Trong tuong lai can nhac API server de client khong connect SQL truc tiep.

### 13.4 SignalR disconnect

Giai phap:

- Auto reconnect.
- DB-first.
- Sau reconnect refresh messages tu DB.

Neu chon MQTT-first, rui ro tuong ung la broker disconnect:

- `MqttRealtimeService` da co best-effort va reconnect cho request/status.
- Can dam bao chat event cung giu contract nay.
- Mat MQTT khong duoc lam fail `ChatService.SendMessageAsync`.

### 13.5 Duplicate message UI

Giai phap:

- UI track `message_id`.
- Neu event den cho message da hien thi thi bo qua.
- MVP co the refresh entire message list tu DB thay vi append thang.

## 14. Do kho danh gia

### Demo cung mot may

Do kho: thap den trung binh.

Chay 2 WPF instance:

```text
Instance 1: Buyer
Instance 2: Seller
SignalR: localhost:5000
SQL: local
```

### Demo cung hotspot/LAN

Do kho: trung binh.

Kho nhat:

- SQL remote.
- Firewall.
- IP may A.
- SignalR bind `0.0.0.0`.

### Demo qua Radmin/NordVPN Meshnet

Do kho: trung binh.

De hon public server, kho hon cung may.

### Public Internet nhu Messenger that

Do kho: trung binh cao den cao.

Can:

- VPS/cloud/tunnel.
- HTTPS/certificate.
- Auth hub tot hon.
- Deploy DB/API.
- Monitoring/logging.

Khong nen chen vao MVP neu chi can demo do an.

## 15. Ke hoach trien khai sau Phase 10

De xuat lam tren fork/branch rieng:

```text
phase8a-signalr-demo
```

Thu tu:

1. Chot realtime mode: MQTT-first hay SignalR-first, ghi ly do vao README/plan.
2. Sua composition root de Connection Mode/IP duoc chon truoc khi tao `SqlConnectionFactory` va realtime settings.
3. Neu MQTT-first: mo rong `IRealtimeNotifier/IRealtimeSubscriber/MqttRealtimeService` cho `MessageCreated`.
4. Neu SignalR-first: tao `CustomKeyboard.ChatHost` voi `ChatHub`, dong thoi tao chat notifier/subscriber ro rang.
5. Chay duoc localhost single-machine.
6. Sua `ChatViewModel` de connect/subscribe/refresh khi co event.
7. Quy dinh ro conversation dang mo only hay per-user channel cho conversation list.
8. Them Test Connection cho SQL + MQTT/SignalR.
9. Test single-machine 2 instance.
10. Test hotspot A/B.
11. Test Radmin/NordVPN Meshnet neu can.
12. Cap nhat diagrams/docs.
13. Them verification fake realtime publish.

Definition of Done:

- Tin nhan van luu DB khi realtime on/off.
- Hai client online thay tin realtime.
- Client offline/reconnect khong mat tin.
- B/C nhap IP A thay vi sua code.
- Diagram/tai lieu phan biet ro DB source-of-truth va MQTT/SignalR realtime delivery.
- Tai lieu tra loi ro vi sao tai dung MQTT hoac vi sao them SignalR song song.
