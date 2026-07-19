using TeamsAITeammate.Core.Models;

namespace TeamsAITeammate.Core.Interfaces;

public interface ICommandParser
{
    CommandResult Parse(string mentionText);
}
