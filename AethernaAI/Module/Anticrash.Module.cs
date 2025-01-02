// using AethernaAI.Enum;
// using AethernaAI.Util;
// using Swan;
// using VRChat.API.Api;
// using VRChat.API.Client;
// using VRChat.API.Model;
// using static AethernaAI.Constants;

// namespace AethernaAI.Module;

// public class AnticrashModule : ApiClient
// {
//   public AnticrashModule(Core core)
//   {
//     if (core is null)
//       throw new ArgumentNullException(nameof(core));

//     _core = core;
//     Initialize();
//   }

//   public bool Logged { get; private set; } = false;
//   private void Initialize()
//   {
//     if (_core is null)
//       throw new NullReferenceException(nameof(_core));

//     _groupId = _core.Config.GetConfig<string?>(config => config.VrchatGroupId!);
//     _worldId = _core.Config.GetConfig<string?>(config => config.VrchatWorldId!);

//     Logger.Log(LogLevel.Info, $"gid: {_groupId}, wid: {_worldId}");

//     _config = new()
//     {
//       Username = _core.Config.GetConfig<string?>(config => config.VrchatUsername!),
//       Password = _core.Config.GetConfig<string?>(config => config.VrchatPassword!),
//       UserAgent = "UnityPlayer/2022.3.22f1-DWR (UnityWebRequest/1.0, libcurl/8.5.0-DEV)"
//     };
//     _config.DefaultHeaders.Add("X-Unity-Version", "2022.3.22f1-DWR");

//     _auth = new(this, this, _config);
//     _usersApi = new(this, this, _config);
//     _groupsApi = new(this, this, _config);
//     _avatarsApi = new(this, this, _config);
//     _instancesApi = new(this, this, _config);

//     try
//     {
//       var user = _auth!.GetCurrentUserWithHttpInfo();
//       var dialog = Create2FADialog(RequiresEmail2FA(user));

//       if (dialog.ShowDialog() == DialogResult.OK)
//       {
//         var code = dialog.Controls.OfType<TextBox>().First().Text;
//         if (code == "Enter code" || string.IsNullOrWhiteSpace(code))
//         {
//           Logger.Log(LogLevel.Warn, "Verification code not provided, disabling module from being usable..");
//           return;
//         }

//         Logger.Log(LogLevel.Step, "Verification code provided, logging to VRChat..");
//         dynamic result = RequiresEmail2FA(user)
//           ? _auth.Verify2FAEmailCode(new TwoFactorEmailCode(code))
//           : _auth.Verify2FA(new TwoFactorAuthCode(code));

//         if (result.Verified && !Logged)
//         {
//           var data = _auth.GetCurrentUser();
//           Logged = true;
//           Logger.Log(LogLevel.Info, $"Anticrash module active on {data.DisplayName}");

//           Update();

//           var inter = new Interval(Update, 60 * 1000);
//           inter.Start();
//         }
//       }
//     }
//     catch (ApiException e)
//     {
//       Logger.Log(LogLevel.Error, $"There's problem with my account: {e.Message}");
//     }
//   }

//   private void UpdateUsers()
//   {
//     if (_instance is not null)
//       return;

//     // updating users as well if event j/l is not enough
//     var users = _instance!.Users.ToList();
//     foreach (var user in users)
//     {
//       if (!_core!.Registry.Users.Has(user.Id))
//       {
//         var now = DateTime.Now;
//         var model = new Model.User()
//         {
//           Id = user.Id,
//           JoinedAt = DateUtil.ToUnixTime(now),
//           LastVisit = DateUtil.ToUnixTime(now),
//           DisplayName = user.DisplayName
//         };

//         _core.Registry.Users.Save(user.Id, model);
//       }
//     }
//   }

//   private void Update()
//   {
//     if (!Logged || _core is null)
//       return;

//     // var a = _avatarsApi.GetOwnAvatar("usr_849ac025-7f46-4645-b23a-ac52fd13dfac");
//     // Console.WriteLine(a.Stringify());
//     // var d = _avatarsApi.SearchAvatars(false, SortOption.Name, null, null, 60);
//     // _world = _worldsApi!.GetWorld(_worldId);

//     // if (_instance is null)
//     // {
//     //   // foreach (var instance in _groupsApi!.GetGroupInstances(_groupId))
//     //   // {
//     //   //   if (instance.World.Id == _worldId)
//     //   //   {
//     //   //     _instance = _instancesApi!.GetInstance(_worldId, instance.);
//     //   //     // Console.WriteLine(instance.ToJson().ToString());
//     //   //     break;
//     //   //   }
//     //   // }
      

//     //   UpdateUsers();
//     // }
//     // else
//     // {
//     //   _instance = _instancesApi!.GetInstance(_worldId, _instance.Id);

//     //   UpdateUsers();
//     // }
//   }

//   // private Group? _group;
//   private World? _world;
//   private string? _groupId;
//   private string? _worldId;
//   private Instance? _instance;
//   private UsersApi? _usersApi;
//   private AvatarsApi? _avatarsApi;
//   private WorldsApi? _worldsApi;
//   private GroupsApi? _groupsApi;
//   private InstancesApi? _instancesApi;

//   private readonly Core? _core;
//   private protected AuthenticationApi? _auth;
//   private protected Configuration? _config;
//   private protected Dialog Create2FADialog(bool email)
//   {
//     var dialog = new Dialog()
//     {
//       Text = $"{(email ? "Email" : "2FA")} verification code required",
//       Width = 300,
//       Height = 150
//     };

//     var code = new TextBox()
//     {
//       Text = "Enter code",
//       Width = 200,
//       Location = new(50, 30),
//       ForeColor = Color.Gray,
//     };

//     code.Enter += (sender, e) =>
//     {
//       if (code.Text == "Enter code")
//       {
//         code.Text = "";
//         code.ForeColor = Color.Black;
//       }
//     };

//     code.Leave += (sender, e) =>
//     {
//       if (string.IsNullOrWhiteSpace(code.Text))
//       {
//         code.Text = "Enter code";
//         code.ForeColor = Color.Gray;
//       }
//     };

//     var btn = new Button()
//     {
//       Text = "Verify",
//       Location = new(110, 70),
//       DialogResult = DialogResult.OK
//     };

//     dialog.Controls.Add(code);
//     dialog.Controls.Add(btn);

//     return dialog;
//   }

//   private bool RequiresEmail2FA(ApiResponse<CurrentUser> resp)
//   {
//     if (resp.RawContent.Contains("emailOtp"))
//       return true;

//     return false;
//   }
// }