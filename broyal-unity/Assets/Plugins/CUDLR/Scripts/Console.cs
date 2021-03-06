using UnityEngine;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Net;

namespace CUDLR {

  struct QueuedCommand {
    public CommandAttribute command;
    public string[] args;
  }

  public class Console {

    // Max number of lines in the console output
    const int MAX_LINES = 100;

    // Maximum number of commands stored in the history
    const int MAX_HISTORY = 50;

    // Prefix for user inputted command
    const string COMMAND_OUTPUT_PREFIX = "> ";

    private static Console instance;
    private CommandTree m_commands;
    private List<string> m_output;
    private List<string> m_history;
    private string m_help;
    private Queue<QueuedCommand> m_commandQueue;

    private Console() {
      m_commands = new CommandTree();
      m_output = new List<string>();
      m_history = new List<string>();
      m_commandQueue = new Queue<QueuedCommand>();

      RegisterAttributes();
    }

    public static Console Instance {
      get {
        if (instance == null) instance = new Console();
        return instance;
      }
    }

    public static void Update() {
      while (Instance.m_commandQueue.Count > 0) {
        QueuedCommand cmd = Instance.m_commandQueue.Dequeue();
        cmd.command.m_callback( cmd.args );
      }
    }

    /* Queue a command to be executed on update on the main thread */
    public static void Queue(CommandAttribute command, string[] args) {
      QueuedCommand queuedCommand = new QueuedCommand();
      queuedCommand.command = command;
      queuedCommand.args = args;
      Instance.m_commandQueue.Enqueue( queuedCommand );
    }

    /* Execute a command */
    public static void Run(string str) {
      if (str.Length > 0) {
        LogCommand(str);
        Instance.RecordCommand(str);
        Instance.m_commands.Run(str);
      }
    }

    /* Clear all output from console */
    [Command("clear", "clears console output", false)]
    public static void Clear() {
      Instance.m_output.Clear();
    }

    /* Print a list of all console commands */
    [Command("help", "prints commands", false)]
    public static void Help() {
      Log($"Commands:{Instance.m_help}");
    }

    /* Find command based on partial string */
    public static string Complete(string partialCommand) {
      return Instance.m_commands.Complete( partialCommand );
    }

    /* Logs user input to output */
    public static void LogCommand(string cmd) {
      Log(COMMAND_OUTPUT_PREFIX+cmd);
    }

    /* Logs string to output */
    public static void Log(string str) {
      Instance.m_output.Add(str);
      if (Instance.m_output.Count > MAX_LINES) 
        Instance.m_output.RemoveAt(0);
    }

    /* Callback for Unity logging */
    public static void LogCallback (string logString, string stackTrace, LogType type) {
      Console.Log(logString);
      if (type != LogType.Log) {
        Console.Log(stackTrace);
      }
    }

    /* Returns the output */
    public static string Output() {
      return string.Join("\n", Instance.m_output.ToArray());
    }

    /* Register a new console command */
    public static void RegisterCommand(string command, string desc, CommandAttribute.Callback callback, bool runOnMainThread = true) {
      if (string.IsNullOrEmpty(command)) {
        throw new Exception("Command String cannot be empty");
      }

      CommandAttribute cmd = new CommandAttribute(command, desc, runOnMainThread)
      {
        m_callback = callback
      };

      Instance.m_commands.Add(cmd);
      Instance.m_help += $"\n{command} : {desc}";
    }

    private void RegisterAttributes() {
      foreach(Type type in Assembly.GetExecutingAssembly().GetTypes()) {

        // FIXME add support for non-static methods (FindObjectByType?)
        foreach(MethodInfo method in type.GetMethods(BindingFlags.Public|BindingFlags.Static)) {
          CommandAttribute[] attrs = method.GetCustomAttributes(typeof(CommandAttribute), true) as CommandAttribute[];
          if (attrs.Length == 0)
            continue;

          CommandAttribute.Callback cb = Delegate.CreateDelegate(typeof(CommandAttribute.Callback), method, false) as CommandAttribute.Callback;
          if (cb == null)
          {
            CommandAttribute.CallbackSimple cbs = Delegate.CreateDelegate(typeof(CommandAttribute.CallbackSimple), method, false) as CommandAttribute.CallbackSimple;
            if (cbs != null) {
              cb = delegate
              {
                cbs();
              };
            }
          }

          if (cb == null) {
            Debug.LogError($"Method {type}.{method.Name} takes the wrong arguments for a console command.");
            continue;
          }

          // try with a bare action
          foreach(CommandAttribute cmd in attrs) {
            if (string.IsNullOrEmpty(cmd.m_command)) {
              Debug.LogError($"Method {type}.{method.Name} needs a valid command name.");
              continue;
            }

            cmd.m_callback = cb;
            m_commands.Add(cmd);
            m_help += $"\n{cmd.m_command} : {cmd.m_help}";
          }
        }
      }
    }
    
    public static bool AddCommandByMethod(Expression<Action> expression)
    {
      var info = SymbolExtensions.GetMethodInfo(expression);
      return info == null || AddCommandByMethodInfo(info);
    }

    public static bool AddCommandByMethodInfo(MethodInfo method)
    {
      CommandAttribute[] attrs = method.GetCustomAttributes(typeof(CommandAttribute), true) as CommandAttribute[];
      if (attrs.Length == 0) return false;
      
      CommandAttribute.Callback cb = Delegate.CreateDelegate(typeof(CommandAttribute.Callback), method, false) as CommandAttribute.Callback;
      if (cb == null)
      {
        CommandAttribute.CallbackSimple cbs = Delegate.CreateDelegate(typeof(CommandAttribute.CallbackSimple), method, false) as CommandAttribute.CallbackSimple;
        if (cbs != null) {
          cb = delegate
          {
            cbs();
          };
        }
      }

      if (cb == null) {
        Debug.LogError($"Method {method.DeclaringType}.{method.Name} takes the wrong arguments for a console command.");
        return false;
      }

      // try with a bare action
      foreach(CommandAttribute cmd in attrs) {
        if (string.IsNullOrEmpty(cmd.m_command)) {
          Debug.LogError($"Method {method.DeclaringType}.{method.Name} needs a valid command name.");
          continue;
        }

        cmd.m_callback = cb;
        Instance.m_commands.Add(cmd);
        Instance.m_help += $"\n{cmd.m_command} : {cmd.m_help}";
      }
      return true;
    }


    /* Get a previously ran command from the history */
    public static string PreviousCommand(int index) {
      return index >= 0 && index < Instance.m_history.Count ? Instance.m_history[index]  : null;
    }

    /* Update history with a new command */
    private void RecordCommand(string command) {
      m_history.Insert(0, command);
      if (m_history.Count > MAX_HISTORY) 
        m_history.RemoveAt(m_history.Count - 1);
    }

    // Our routes
    [Route("^/console/out$")]
    public static void Output(RequestContext context) {
      context.Response.WriteString(Console.Output());
    }

    [Route("^/console/run$")]
    public static void Run(RequestContext context) {
      string command = context.Request.QueryString.Get("command");
      if (!string.IsNullOrEmpty(command))
        Console.Run(command);

      context.Response.StatusCode = (int)HttpStatusCode.OK;
      context.Response.StatusDescription = "OK";
    }

    [Route("^/console/commandHistory$")]
    public static void History(RequestContext context) {
      string index = context.Request.QueryString.Get("index");

      string previous = null;
      if (!string.IsNullOrEmpty(index))
        previous = Console.PreviousCommand(System.Int32.Parse(index));

      context.Response.WriteString(previous);
    }

    [Route("^/console/complete$")]
    public static void Complete(RequestContext context) {
      string partialCommand = context.Request.QueryString.Get("command");

      string found = null;
      if (partialCommand != null)
        found = Console.Complete(partialCommand);

      context.Response.WriteString(found);
    }
  }

  class CommandTree {

    private Dictionary<string, CommandTree> m_subcommands;
    private CommandAttribute m_command;

    public CommandTree() {
      m_subcommands = new Dictionary<string, CommandTree>();
    }

    public void Add(CommandAttribute cmd) {
      _add(cmd.m_command.ToLower().Split(' '), 0, cmd);
    }

    private void _add(string[] commands, int command_index, CommandAttribute cmd) {
      if (commands.Length == command_index) {
        m_command = cmd;
        return;
      }

      string token = commands[command_index];
      if (!m_subcommands.ContainsKey(token)){
        m_subcommands[token] = new CommandTree();
      }
      m_subcommands[token]._add(commands, command_index + 1, cmd);
    }

    public string Complete(string partialCommand) {
      return _complete(partialCommand.Split(' '), 0, "");
    }

    public string _complete(string[] partialCommand, int index, string result) {
      if (partialCommand.Length == index && m_command != null) {
        // this is a valid command... so we do nothing
        return result;
      } else if (partialCommand.Length == index) {
        // This is valid but incomplete.. print all of the subcommands
        Console.LogCommand(result);
        foreach (string key in m_subcommands.Keys) {
          Console.Log( result + " " + key);
        }
        return result + " ";
      } else if (partialCommand.Length == (index+1)) {
        string partial = partialCommand[index];
        if (m_subcommands.ContainsKey(partial)) {
          result += partial;
          return m_subcommands[partial]._complete(partialCommand, index+1, result);
        }

        // Find any subcommands that match our partial command
        List<string> matches = new List<string>();
        foreach (string key in m_subcommands.Keys) {
          if (key.StartsWith(partial)) {
            matches.Add(key);
          }
        }

        if (matches.Count == 1) {
          // Only one command found, log nothing and return the complete command for the user input
          return result + matches[0] + " ";
        } else if (matches.Count > 1) {
          // list all the options for the user and return partial
          Console.LogCommand(result + partial);
          foreach (string match in matches) {
            Console.Log( result + match);
          }
        }
        return result + partial;
      }

      string token = partialCommand[index];
      if (!m_subcommands.ContainsKey(token)) {
        return result;
      }
      result += token + " ";
      return m_subcommands[token]._complete( partialCommand, index + 1, result );
    }

    public void Run(string commandStr) {
      // Split user input on spaces ignoring anything in qoutes
      Regex regex = new Regex(@""".*?""|[^\s]+");
      MatchCollection matches = regex.Matches(commandStr);
      string[] tokens = new string[matches.Count];
      for (int i = 0; i < tokens.Length; ++i) {
        tokens[i] = matches[i].Value.Replace("\"","");
      }
      _run(tokens, 0);
    }

    static string[] emptyArgs = new string[0]{};
    private void _run(string[] commands, int index) {
      if (commands.Length == index) {
        RunCommand(emptyArgs);
        return;
      }

      string token = commands[index].ToLower();
      if (!m_subcommands.ContainsKey(token)) {
        RunCommand(commands.Skip(index).ToArray());
        return;
      }
      m_subcommands[token]._run(commands, index + 1);
    }

    private void RunCommand(string[] args) {
      if (m_command == null) {
        Console.Log("command not found");
      } else if (m_command.m_runOnMainThread) {
        Console.Queue( m_command, args );
      } else {
        m_command.m_callback(args);
      }
    }
  }
}