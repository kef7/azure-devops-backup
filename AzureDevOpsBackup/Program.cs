using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Collections.Generic;
using Newtonsoft.Json;
using RestSharp;
using RestSharp.Extensions;
using System.Diagnostics;

namespace AzureDevOpsBackup
{
	struct Project
	{
		public string name;
	}
	struct Projects
	{
		public List<Project> value;
	}
	struct Branch
	{
		public string name;
		public string objectId;
		public string url;
	}
	struct Branchs
	{
		public List<Branch> value;
	}
	struct Repo
	{
		public string id;
		public string name;
		public string remoteUrl;

	}
	struct Repos
	{
		public List<Repo> value;
	}
	struct Item
	{
		public string objectId;
		public string gitObjectType;
		public string commitId;
		public string path;
		public bool isFolder;
		public string url;
	}
	struct Items
	{
		public int count;
		public List<Item> value;
	}
	class Program
	{
		static void Main(string[] args)
		{
			string[] requiredArgs = { "--token", "--organization", "--outdir" };
			if (args.Intersect(requiredArgs).Count() == 3)
			{
				const string version = "api-version=5.1";//"api-version=5.1-preview.1";
				var base64EncodedPat = Convert.ToBase64String(System.Text.ASCIIEncoding.ASCII.GetBytes(string.Format("{0}:{1}", "", args[Array.IndexOf(args, "--token") + 1])));
				string auth = "Basic " + base64EncodedPat;
				string baseURL = "https://dev.azure.com/" + args[Array.IndexOf(args, "--organization") + 1] + "/";
				string outDir = args[Array.IndexOf(args, "--outdir") + 1] + "\\";
				var clientProjects = new RestClient(baseURL + "_apis/projects?" + version);
				var requestProjects = new RestRequest(Method.GET);
				requestProjects.AddHeader("Authorization", auth);
				IRestResponse responseProjects = clientProjects.Execute(requestProjects);
				Projects projects = JsonConvert.DeserializeObject<Projects>(responseProjects.Content);
				foreach (Project project in projects.value)
				{
					Console.WriteLine(project.name);
					var clientRepos = new RestClient(baseURL + project.name + "/_apis/git/repositories?" + version);
					var requestRepos = new RestRequest(Method.GET);
					requestRepos.AddHeader("Authorization", auth);
					IRestResponse responseRepos = clientRepos.Execute(requestRepos);
					Repos repos = JsonConvert.DeserializeObject<Repos>(responseRepos.Content);
					//               foreach (Repo repo in repos.value)
					//               {
					//                   Console.Write("\n\t" + repo.name);
					//                   var clientItems = new RestClient(baseURL + "_apis/git/repositories/" + repo.id + "/items?recursionlevel=full&" + version);
					//                   var requestItems = new RestRequest(Method.GET);
					//                   requestItems.AddHeader("Authorization", auth);
					//                   IRestResponse responseItems = clientItems.Execute(requestItems);
					//                   Items items = JsonConvert.DeserializeObject<Items>(responseItems.Content);
					//                   Console.Write(" - " + items.count + "\n");
					//                   if (items.count > 0)
					//                   {
					//                       var clientBlob = new RestClient(baseURL + "_apis/git/repositories/" + repo.id + "/blobs?" + version);
					//                       var requestBlob = new RestRequest(Method.POST);
					//                       requestBlob.AddJsonBody(items.value.Where(itm => itm.gitObjectType == "blob").Select(itm => itm.objectId).ToList());
					//                       requestBlob.AddHeader("Authorization", auth);
					//                       requestBlob.AddHeader("Accept", "application/zip");
					//                       clientBlob.DownloadData(requestBlob).SaveAs(outDir + project.name + "_" + repo.name + "_blob.zip");
					//                       File.WriteAllText(outDir + project.name + "_" + repo.name + "_tree.json", responseItems.Content);
					//                       if (Array.Exists(args, argument => argument == "--unzip"))
					//                       {
					//                           if (Directory.Exists(outDir + project.name + "_" + repo.name)) Directory.Delete(outDir + project.name + "_" + repo.name, true);
					//                           Directory.CreateDirectory(outDir + project.name + "_" + repo.name);
					//                           ZipArchive archive = ZipFile.OpenRead(outDir + project.name + "_" + repo.name + "_blob.zip");
					//                           foreach (Item item in items.value)
					//                               if (item.isFolder) Directory.CreateDirectory(outDir + project.name + "_" + repo.name + item.path);
					//                               else archive.GetEntry(item.objectId).ExtractToFile(outDir + project.name + "_" + repo.name + item.path, true);
					//                       }
					//	}
					//}

					// Clone repos
					// TODO: Test if git installed
					if (Array.Exists(args, argument => argument == "--clone"))
					{
						// Create clone dir
						var cloneDir = outDir + "_cloned";
						if (!Directory.Exists(cloneDir))
						{
							Directory.CreateDirectory(cloneDir);
						}

						// Clone all git repos
						var gitCloneCmdFmtStr = "git -c http.extraheader=\"AUTHORIZATION: Basic {0}\" clone \"{1}\" \"{2}\"";
						foreach (var repo in repos.value)
						{
							// Create dir for repo
							var repoDir = cloneDir + "\\" + repo.name;
							if (!Directory.Exists(repoDir))
							{
								Directory.CreateDirectory(repoDir);
							}

							// Clone repo into its dir
							var gitCloneCmd = string.Format(gitCloneCmdFmtStr, base64EncodedPat, repo.remoteUrl, repoDir);
							Console.WriteLine("\t{0}", gitCloneCmd);
							var gitProcess = new Process();
							try
							{
								// Setup process start info for clone
								var gitProcessStartInfo = new ProcessStartInfo();
								gitProcessStartInfo.CreateNoWindow = true;
								gitProcessStartInfo.UseShellExecute = false;
								gitProcessStartInfo.RedirectStandardError = true;
								gitProcessStartInfo.RedirectStandardOutput = true;
								gitProcessStartInfo.FileName = "cmd.exe";
								gitProcessStartInfo.Arguments = "/c " + gitCloneCmd;
								gitProcessStartInfo.WorkingDirectory = cloneDir;

								// Start git process
								gitProcess.StartInfo = gitProcessStartInfo;
								gitProcess.Start();
								var stderr = gitProcess.StandardError.ReadToEnd();
								var stdout = gitProcess.StandardOutput.ReadToEnd();
								gitProcess.WaitForExit();
								gitProcess.Close();

								// Handle out
								if (!string.IsNullOrWhiteSpace(stderr))
								{
									Console.WriteLine(stderr);
								}
								if (!string.IsNullOrWhiteSpace(stdout))
								{
									Console.WriteLine(stdout);
								}

								// Get all branches
								var gitSyncProcess = new Process();
								try
								{
									/*
									https://dev.azure.com/kef7/WGU/_apis/git/repositories/C777_Examples/refs?includeLinks=true&includeStatuses=false&includeMyBranches=true&latestStatusesOnly=false&peelTags=false&api-version=4.1
									*/
									var clientRepoBranches = new RestClient(baseURL + project.name + "/_apis/git/repositories/" + repo.name + "/refs?includeLinks=true&includeStatuses=false&includeMyBranches=true&latestStatusesOnly=false&peelTags=false" + version);
									var requestRepoBranches = new RestRequest(Method.GET);
									requestRepoBranches.AddHeader("Authorization", auth);
									IRestResponse responseRepoBranches = clientRepoBranches.Execute(requestRepoBranches);
									var repoBranches = JsonConvert.DeserializeObject<Branchs>(responseRepoBranches.Content);
									foreach (var branch in repoBranches.value)
									{
										var branchName = branch.name.Replace("refs/heads/", "");

										// Setup process start info for fetch and pull
										var gitSyncProcessStartInfo = new ProcessStartInfo();
										gitSyncProcessStartInfo.CreateNoWindow = true;
										gitSyncProcessStartInfo.UseShellExecute = false;
										gitSyncProcessStartInfo.RedirectStandardError = true;
										gitSyncProcessStartInfo.RedirectStandardOutput = true;
										gitSyncProcessStartInfo.FileName = "cmd.exe";
										gitSyncProcessStartInfo.Arguments = string.Format("/c \"git fetch {0} && git checkout {0}\"", branchName);
										gitSyncProcessStartInfo.WorkingDirectory = repoDir;

										// Start git process
										gitSyncProcess.StartInfo = gitSyncProcessStartInfo;
										gitSyncProcess.Start();
										var stderrSync = gitSyncProcess.StandardError.ReadToEnd();
										var stdoutSync = gitSyncProcess.StandardOutput.ReadToEnd();
										gitSyncProcess.WaitForExit();
										gitSyncProcess.Close();

										// Handle out
										if (!string.IsNullOrWhiteSpace(stderrSync))
										{
											Console.WriteLine(stderrSync);
										}
										if (!string.IsNullOrWhiteSpace(stdoutSync))
										{
											Console.WriteLine(stdoutSync);
										}
									}
								}
								catch (Exception ex)
								{
									Console.WriteLine(ex.Message);
								}
								finally
								{
									if (gitSyncProcess != null)
									{
										gitSyncProcess.Dispose();
										gitSyncProcess = null;
									}
								}
							}
							catch (Exception ex)
							{
								Console.WriteLine(ex.Message);
							}
							finally
							{
								if (gitProcess != null)
								{
									gitProcess.Dispose();
									gitProcess = null;
								}
							}
						}
					}
				}
			}
		}
	}
}
