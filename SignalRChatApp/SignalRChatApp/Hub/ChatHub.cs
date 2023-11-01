using Microsoft.AspNetCore.SignalR;

namespace SignalRChatApp.Hub;

public class ChatHub:Microsoft.AspNetCore.SignalR.Hub
{
    private readonly IDictionary<string, UserConnection> _connection;

    public ChatHub(IDictionary<string, UserConnection> connection)
    {
        _connection = connection;
    }
    public async Task JoinRoomAsync(UserConnection userConnection)
    {
        
        await Groups.AddToGroupAsync(Context.ConnectionId, userConnection.Room!);
        //koleksiyona eklenen user ı kaydederiz.
        _connection[Context.ConnectionId] = userConnection;
        //eğer user odada değil ve yeni katılıyorsa odadakilere userın geldiğine dair mesaj göndeririz.
        await Clients.Group(userConnection.Room!)
            .SendAsync("ReceiveMessage","Annotation",$"{userConnection.User} gruba katıldı.",DateTime.Now);
        //odadaki userları güncelleriz
        await SendConnectedUser(userConnection.Room!);
    }

    public async Task SendMessageAsync(string message)
    {
        //dictionary yapısının verdiği TryGetValue ile ilişkili user'ın sahip olduğu(Hub'dan gelen) uniq Id yi kullanarak,
        //userConnection a atar, sonuç olarak ilişkilendirilmiş user var ise metot içindeki işlemleri gerçekleştirir.
        if (_connection.TryGetValue(Context.ConnectionId, out UserConnection userConnection))
        {
            //Burada kullanıcının gönderdiği mesajı diğer odadaki üyelere göndermeye yarar. Ön taraftan çağırılan  
            //"ReceiveMessage" ile gerekli işlemler yapılarak burada ilişkili user a ait mesaj işlenir. Ve geri kalanlar,time bilgisi vs.
            await Clients.Group(userConnection.Room!)
                .SendAsync("ReceiveMessage", userConnection.User, message, DateTime.Now);
        }
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        //Hub'dan gelen metotu override ederek, kullanıcının disconnect olma durumunu yine ilgili userı çekerek gerçekleştiriyoruz.
        if (_connection.TryGetValue(Context.ConnectionId, out UserConnection userConnection))
        {
            //base gönderiyoruz
            await base.OnDisconnectedAsync(exception);
        }
        //kullanıcının bilgisini contextren kaldırıyoruz/siliyoruz.
        _connection.Remove(Context.ConnectionId);

        //grupta ki diğer kullanıcılara ön tarafta çağırılıp işlenen "ReceiveMessage" ile bildirim veriyoruz.
        await Clients.Group(userConnection.Room!)
            .SendAsync("ReceiveMessage", $"{userConnection.User} gruptan ayrıldı!");
        
        //odadaki userları güncelliyoruz
        await SendConnectedUser(userConnection.Room!);

    }

    public Task SendConnectedUser(string room)
    {
        //koleksiyonumuzdaki bilgilerden önce parametre olarak gelen oda bilgisi ile eşleştirdikten sonra kullanıcıları listeliyoruz
        var users = _connection.Values
            .Where(u => u.Room == room)
            .Select(s => s.User);

        //ön taraftan çağırarak işlediğimmiz "ConnectedUser" metotu ile odadaki user bilgisi ile ilgili bilgiyi işliyoruz
        return Clients.Group(room).SendAsync("ConnectedUser", users);
    }
    
    
}