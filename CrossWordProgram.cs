using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using log4net;
using log4net.Config;

namespace CrossWord
{
    static class CrossWordProgram
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(CrossWordProgram));

        public static int Main(string[] args)
        {
            InitLog();
            var version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString();
            Log.InfoFormat("CrossWord ver. {0} ", version);

            string inputFile, outputFile, puzzle, dictionaryFile;
            if (!ParseInput(args, out inputFile, out outputFile, out puzzle, out dictionaryFile))
            {
                return 1;
            }
            ICrossBoard board;
            try
            {
                board = CrossBoardCreator.CreateFromFile(inputFile);
            }
            catch (Exception e)
            {
                Log.Error(string.Format("Cannot load crossword layout from file {0}.", inputFile), e);
                return 2;
            }
            Dictionary dictionary;
            try
            {
                dictionary = new Dictionary(dictionaryFile, board.MaxWordLength);
            }
            catch (Exception e)
            {
                Log.Error(string.Format("Cannot load dictionary from file {0}.", dictionaryFile), e);
                return 3;
            }
            ICrossBoard resultBoard;
            try
            {
                resultBoard = GenerateFirstCrossWord(board, dictionary, puzzle);
            }
            catch (Exception e)
            {
                Log.Error("Generating crossword has failed.", e);
                return 4;
            }
            if (resultBoard == null)
            {
                Log.Error(string.Format("No solution has been found."));
                return 5;
            }
            try
            {
                SaveResultToFile(outputFile, resultBoard, dictionary);
            }
            catch (Exception e)
            {
                Log.Error(string.Format("Saving result crossword to file {0} has failed.", outputFile), e);
                return 6;
            }
            return 0;
        }

        static void InitLog()
        {
            var x = new log4net.Appender.ConsoleAppender { Layout = new log4net.Layout.PatternLayout("%message%newline") };
            BasicConfigurator.Configure(x);
        }

        static bool ParseInput(IEnumerable<string> args, out string inputFile, out string outputFile, out string puzzle,
            out string dictionary)
        {
            bool help = false;
            string i = null, o = null, p = null, d = null;
            var optionSet = new NDesk.Options.OptionSet
                                {
                                    { "i|input=", "(input file)", v => i = v },
                                    { "d|dictionary=", "(dictionary)", v => d = v },
                                    { "o|output=", "(output file)", v => o = v },
                                    { "p|puzzle=", "(puzze)", v => p = v },
                                    { "h|?|help", "(help)", v => help = v != null },
                                };
            var unparsed = optionSet.Parse(args);
            inputFile = i;
            outputFile = o;
            puzzle = p;
            dictionary = d;
            if (help || unparsed.Count > 1 || string.IsNullOrEmpty(inputFile) ||
                string.IsNullOrEmpty(outputFile) || string.IsNullOrEmpty(puzzle) ||
                string.IsNullOrEmpty(dictionary))
            {
                optionSet.WriteOptionDescriptions(Console.Out);
                return false;
            }
            return true;
        }

        static ICrossBoard GenerateFirstCrossWord(ICrossBoard board, ICrossDictionary dictionary, string puzzle)
        {
            var placer = new PuzzlePlacer(board, puzzle);
            var cts = new CancellationTokenSource();
            var mre = new ManualResetEvent(false);
            ICrossBoard successFullBoard = null;
            foreach (var boardWithPuzzle in placer.GetAllPossiblePlacements(dictionary))
            {
                //boardWithPuzzle.WriteTo(new StreamWriter(Console.OpenStandardOutput(), Console.OutputEncoding) { AutoFlush = true });
                var gen = new CrossGenerator(dictionary, boardWithPuzzle);
                var t = Task.Factory.StartNew(() =>
                                          {
                                              if (gen.Generate())
                                              {
                                                  successFullBoard = gen.Board;
                                                  cts.Cancel();
                                                  mre.Set();
                                              }
                                          }, cts.Token);
                if (cts.IsCancellationRequested)
                    break;
            }
            mre.WaitOne();
            return successFullBoard;
        }

        static void SaveResultToFile(string outputFile, ICrossBoard resultBoard, ICrossDictionary dictionary)
        {
            Log.Info("Solution has been found:");
            using (var writer = new StreamWriter(new FileStream(outputFile, FileMode.Create)))
            {
                resultBoard.WriteTo(writer);
                resultBoard.WritePatternsTo(writer, dictionary);
            }
        }
    }
}