using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace BilibiliLivechatWindowedPlugin
{
	/// <summary>
	/// Interaction logic for MainWindow.xaml
	/// </summary>
	public partial class MainWindow : Window
	{
		public MainWindow()
		{
			InitializeComponent();

			System.Net.ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls11 | SecurityProtocolType.Tls12;
		}

		static bool isCommissionEnabled = false;
		static bool isDown = true; 
		static Tuple<string?, string?, string?>? lastMessage = null;
		static Queue<Tuple<string, string, string>> songs = new();

		static HttpClient client = new(new HttpClientHandler() { UseProxy = false });
		private void BilibiliDanmuLookup(string roomid)
		{
			new Thread(() =>
			{

				
				new Thread(async () =>
				{
					while (!isDown)
					{
						try
						{
							string ret = await client.GetStringAsync($"https://api.live.bilibili.com/xlive/web-room/v1/dM/gethistory?roomid={roomid}");
							//string ret = await client.GetStringAsync($"https://cn.bing.com/");
							JsonDocument jDocument = JsonDocument.Parse(ret);
							JsonElement dataArray = jDocument.RootElement.GetProperty("data").GetProperty("room");
							JsonElement element = dataArray[dataArray.GetArrayLength() - 1];
							if (lastMessage is null || lastMessage.Item3 != element.GetProperty("timeline").GetString())
							{
								lastMessage = new(
									element.GetProperty("nickname").GetString(),
									element.GetProperty("text").GetString(),
									element.GetProperty("timeline").GetString());
								Log($"({lastMessage.Item3}) {lastMessage.Item1}: {lastMessage.Item2}");
								if (lastMessage.Item2!.Contains("点歌") && isCommissionEnabled)
								{
									songs.Enqueue(lastMessage!);
									OnUIThread(() => {
										labelRemainingCommissions.Content =
										songs.Count > 0 ? $"当前剩余{songs.Count}首" : $"当前无剩余点歌";
									});
									Log($"接收到点歌请求: {lastMessage}");
								}
							}
						}
						catch (Exception e)
						{
							Log(e.ToString());
							lastMessage = new("Exception", e.ToString(), "");
						}
						Thread.Sleep(5000);
					}
				}).Start();
				while (!isDown)
				{

					try
					{
						if (!Directory.Exists(@"C:\AdofaiExternalPlugins"))
						{
							Directory.CreateDirectory(@"C:\AdofaiExternalPlugins");
						}

						if (lastMessage is not null && lastMessage.Item1 is not null)
						{
							string toSend = "";

							toSend = $"({lastMessage.Item3}) {lastMessage.Item1}: {lastMessage.Item2}";

							if (isCommissionEnabled)
							{
								if (songs.Count > 0)
								{
									toSend += $" | 点歌剩余{songs.Count}首 | {songs.First().Item1}: {songs.First().Item2}";
								}
								else
								{
									toSend += $" | 点歌剩余{songs.Count}首 | 当前无点歌，发送\"点歌\"+内容 点歌";
								}
							}
							File.WriteAllText(@"C:\AdofaiExternalPlugins\Connection.txt", toSend);
							Thread.Sleep(new Random().Next(400, 600));
							// Log(toSend);
						}
					}
					catch (IOException) { }
					catch (Exception e) { Console.WriteLine(e); }


				}
			}).Start();
		}

		private void Log(string log)
		{
			OnUIThread(() => 
			{ 
				if (logbox.Document.Blocks.Count > 514)
				{
					ClearLogs(new(), new());
				}
				logbox.Document.Blocks.Add(new Paragraph(new Run($"{DateTime.Now} | {log}"))); 
			});
		}

		private void OnUIThread(Action action)
		{
			if (Dispatcher.CheckAccess())
			{
				action();
			}
			else
			{
				Dispatcher.Invoke(action);
			}
		}

		private void ClearLogs(object sender, RoutedEventArgs e)
		{
			logbox.Document.Blocks.Clear();
		}

		private void buttonAcceptDanmaku_Click(object sender, RoutedEventArgs e)
		{
			if (isDown)
			{
				isDown = false;
				buttonAcceptDanmaku.Content = "停止接收弹幕";
				inputboxLiveId.IsEnabled = false;
				BilibiliDanmuLookup(inputboxLiveId.Text);

				Log($"开始接收直播间号为{inputboxLiveId.Text}的弹幕");
			}
			else
			{
				isDown = true;
				buttonAcceptDanmaku.Content = "开始接收弹幕";
				inputboxLiveId.IsEnabled = true;

				Log("停止接收弹幕");
			}
		}

		private void buttonAcceptCommission_Click(object sender, RoutedEventArgs e)
		{
			if (isCommissionEnabled)
			{
				isCommissionEnabled = false;
				buttonAcceptCommission.Content = "开启点歌功能";
				
				buttonNextCommission.IsEnabled = false;
				buttonNextCommission.Visibility = Visibility.Hidden;
				labelRemainingCommissions.IsEnabled = false;
				labelRemainingCommissions.Visibility = Visibility.Hidden;



				Log("点歌功能关闭");
			}
			else
			{
				isCommissionEnabled = true;
				buttonAcceptCommission.Content = "关闭点歌功能";

				buttonNextCommission.IsEnabled = true;
				buttonNextCommission.Visibility = Visibility.Visible;
				labelRemainingCommissions.IsEnabled = true;
				labelRemainingCommissions.Visibility = Visibility.Visible;

				Log("点歌功能开启");
			}
		}

		private void NextSongButtonClick(object sender, RoutedEventArgs e)
		{
			songs.TryDequeue(out var dequeued);
			if (dequeued is not null)
			{
				Log($"歌曲结束: {dequeued}");
			}
			else
			{
				Log("点歌清单已经为空!");
			}
			labelRemainingCommissions.Content = songs.Count > 0 ? $"当前剩余{songs.Count}首" : $"当前无剩余点歌";
		}

		private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
		{
			isDown = true;
			client.Dispose();
			File.Delete(@"C:\AdofaiExternalPlugins\Connection.txt");
		}
	}


}