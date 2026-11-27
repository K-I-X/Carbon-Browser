namespace Carbon;

// Carbon AI's one-time download (CarbonAi.cs): started in the background at the first start, and again by Install in the
// AI list if it ever failed. Its progress shows on Carbon AI's row and in setup; a message sent meanwhile waits for it.
public partial class MainWindow
{
 async Task PrepareCarbonAi()
 {
  var cli = CarbonAi.Adapter;
  if (CarbonAi.Ready) { await LoadAiModels(cli); return; }
  aiStatus[cli.Id] = "installing"; aiSetupLine = ""; PublishAiEverywhere();
  bool ok = await CarbonAi.Install(() => Dispatcher.BeginInvoke(() => { aiSetupLine = CarbonAi.Progress; PublishAiEverywhere(); }));
  aiStatus.Remove(cli.Id); aiSetupLine = "";
  await CheckAi(cli);
  if (!ok) aiError = CarbonAi.Error;
  else { aiError = ""; aiModels.Remove(cli.Id); await LoadAiModels(cli); await RunPending(); }
  PublishAiEverywhere();
 }
}
