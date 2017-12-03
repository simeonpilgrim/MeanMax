using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CodinGameFramework {
	public class PlayerState {
		internal Process pro;
		internal int player_id;
		internal string code_file_name;
		public PlayerState(Process _pro, string _code_file_name, int _player_id) {
			pro = _pro;
			code_file_name = _code_file_name;
			player_id = _player_id;
		}
	}



	class Program {
        static PlayerState CreatePlayerState(int id, string botcode) {
            var process = new Process();
            if (Path.GetExtension(botcode).ToLower() == ".py")
            {
                process.StartInfo.FileName = "python.exe";
                process.StartInfo.Arguments = $"\"{botcode}\"";
            }
            else if (Path.GetExtension(botcode).ToLower() == ".exe")
            {
                process.StartInfo.FileName = $"\"{botcode}\"";
                process.StartInfo.Arguments = "";
            }
            else
            {
                throw new InvalidInputException($"Cannot handle \"{botcode}\" as a bot", botcode);
            }
            process.StartInfo.UseShellExecute = false;
			process.StartInfo.RedirectStandardOutput = true;
			process.StartInfo.RedirectStandardError = true;
			process.StartInfo.RedirectStandardInput = true;
			process.ErrorDataReceived += new DataReceivedEventHandler(OutputHandlerError);

			PlayerState ps = new PlayerState(process, botcode, id);

			process.Start();
			process.BeginErrorReadLine();

			return ps;
		}

		static void OutputHandlerError(object sendingProcess, DataReceivedEventArgs outLine) {
			//Console.WriteLine($"E: {outLine.Data}");
		}

		static List<CodePair> Tumbles(List<Code> code) {
			List<CodePair> cps = new List<CodePair>();
			for(int a = 0; a < code.Count; a++) {
				for (int b = a + 1; b < code.Count; b++) {
					for (int c = b + 1; c < code.Count; c++) {
						CodePair cp = new CodePair();
						cp.A = code[a];
						cp.B = code[b];
						cp.C = code[c];
						cps.Add(cp);
					}
				}
			}
			return cps;
		}


		class CodePair {
			internal Code A;
			internal Code B;
			internal Code C;
			internal int A_p = 0;
			internal int B_p = 0;
			internal int C_p = 0;
			internal Code GetCode(int idx) {
				switch (idx) {
				case 0: return A;
				case 1: return B;
				case 2: return C;
				default: throw new Exception();
				}
			}
			internal void AddPoints(int idx, int points) {
				switch (idx) {
				case 0: A_p += points; break;
				case 1: B_p += points; break;
				case 2: C_p += points; break;
				default: throw new Exception();
				}
			}
		}



		internal class Code {
			internal string file;
			internal string name;
			internal int score;
			internal int games;
			internal Code(string _file) {
				file = _file;
				name = Path.GetFileNameWithoutExtension(_file);
				score = 0;
				games = 0;
			}
		}

		static int[][] permutation3 = { new[]{ 0, 1, 2 }, new[]{ 0, 2, 1 }, new[] { 1, 0, 2 }, new[] { 1, 2, 0 }, new[] { 2, 0, 1 }, new[] { 2, 1, 0 } };

		static string CodeDir = @"C:\temp\meanmax\";
		static string DataDir = @"C:\temp\meanmax\";
		static string BigRecordFileName = @"big_battle_log.txt";

		static void RunCodePair(CodePair cp, int loops) {
			string pair_log = Path.Combine(DataDir, $"battles_{cp.A.name}_{cp.B.name}_{cp.C.name}.log");
			using (StreamWriter log = new StreamWriter(pair_log)) {
				for (int i = 0; i < loops; i++) {
					string seedtxt = "";
					int seed;

					foreach(var p in permutation3) { 
						var res = Battle(i, cp.GetCode(p[0]).file, cp.GetCode(p[1]).file, cp.GetCode(p[1]).file, log, seedtxt, out seed);
						cp.AddPoints(p[0], res[0]);
						cp.AddPoints(p[1], res[1]);
						cp.AddPoints(p[2], res[2]);
						seedtxt = seed.ToString();
						Console.WriteLine($"loop: {i}  a: {cp.A_p} b: {cp.B_p}  c: {cp.C_p} {Path.GetFileNameWithoutExtension(cp.A.file)} {Path.GetFileNameWithoutExtension(cp.B.file)} {Path.GetFileNameWithoutExtension(cp.C.file)}");
					}
				}

				string ll = $"Final: {cp.A_p} {cp.B_p} {cp.C_p} {cp.A.name} {cp.B.name} {cp.C.name}";
				log.WriteLine(ll);
				using (StreamWriter big_log = new StreamWriter(Path.Combine(DataDir,BigRecordFileName), true)) {
					big_log.WriteLine(ll);
				}
			}
		}

		static void Main(string[] args) {
			string[] code_files = {
                "simeon_final.py",
                "su_shing_bot.exe",
                "su_shing_bot_edit.exe",
                "nicks_code.py",
                "james_code.py",
                "johns_bot.exe",
            };

			List<Code> codes = new List<Code>();
			foreach(var cf in code_files) {
				codes.Add(new Code(Path.Combine(CodeDir, cf)));
			}

			foreach( var cp in Tumbles(codes)) {
				RunCodePair(cp, 30);
			}
		}

		static int[] Battle(int loop, string codeA, string codeB, string codeC, StreamWriter log, string use_seed, out int used_seed) {
			List<PlayerState> players = new List<PlayerState>();
			players.Add(CreatePlayerState(0, codeA));
			players.Add(CreatePlayerState(1, codeB));
			players.Add(CreatePlayerState(2, codeC));

			MeanMax.Referee _ref = new MeanMax.Referee();
			Properties props = new Properties();
			if (use_seed != "")
				props.Add("seed", use_seed);

			_ref.initReferee(3, props);

			foreach (var p in players) {
				foreach (string l in _ref.getInitInputForPlayer(p.player_id))
					p.pro.StandardInput.WriteLine(l);
			}

			int round = 0;
			while (_ref.isGameOver() == false && round < _ref.getMaxRoundCount(2)) {
				_ref.prepare(round);

				foreach (var p in players) {
					if (_ref.isPlayerDead(p.player_id) == false) {
						foreach (string l in _ref.getInputForPlayer(round, p.player_id)) {
							//Console.WriteLine($"W{p.player_id} {round}:{l}");
							p.pro.StandardInput.WriteLine(l);
						}

						var input = new List<string>();
						while (input.Count < _ref.getExpectedOutputLineCountForPlayer(p.player_id)) {
							string line = p.pro.StandardOutput.ReadLine();
							if (line != null && line.Length > 0)
								input.Add(line);
						}

						foreach (var l in input) {
							//Console.WriteLine($"o{p.player_id} {round}:{l}");
						}
						_ref.handlePlayerOutput(0, round, p.player_id, input.ToArray());
					}
				}
				_ref.updateGame(round);

				//Console.WriteLine($"*{round} {_ref.getScore(0)} {_ref.getScore(1)}");
				round += 1;
			}
			used_seed = _ref.seed;
			int sa = _ref.getScore(0);
			int sb = _ref.getScore(1);
			int sc = _ref.getScore(2);

			string lline = $"loop: {loop} seed: {_ref.seed} round: {round} a: {sa} b: {sb}  c: {sc} {Path.GetFileNameWithoutExtension(codeA)} {Path.GetFileNameWithoutExtension(codeB)} {Path.GetFileNameWithoutExtension(codeC)}";
			log.WriteLine(lline);

			foreach (var p in players) {
				//p.pro.CancelErrorRead();
				//p.pro.StandardOutput.ReadToEnd();
                p.pro.Kill();
                p.pro.WaitForExit();
                p.pro.Close();
			}

			int ra = 0, rb=0, rc = 0;
			
			ra += sa >= sb ? 1 : 0;
			ra += sa >= sc ? 1 : 0;
			rb += sb >= sa ? 1 : 0;
			rb += sb >= sc ? 1 : 0;
			rc += sc >= sa ? 1 : 0;
			rc += sc >= sb ? 1 : 0;

			bat_res[0] = ra;
			bat_res[1] = rb;
			bat_res[2] = rc;

			return bat_res;
		}
		static int[] bat_res = new int[] { 0, 0, 0 };

	}


	public class Properties {
		Dictionary<string, string> values = new Dictionary<string, string>();
		public string getProperty(string name, string defaultValue) {
			string val;
			if (values.TryGetValue(name, out val))
				return val;
			return defaultValue;
		}

		public void Add(string name, string value) {
			values[name] = value;
		}
	}

	public class GameException : Exception {

	}

	public class LostException : GameException {
		public LostException(string msg, object x, object y) {

		}
		public LostException(string msg, object cmd) {

		}
	}

	public class InvalidInputException : GameException {
		public InvalidInputException(string expected, object a) {

		}
	}
}
