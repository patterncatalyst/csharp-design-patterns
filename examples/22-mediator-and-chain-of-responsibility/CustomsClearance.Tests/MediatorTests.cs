using Xunit;

namespace CustomsClearance.Tests;

public class MediatorTests
{
    [Fact]
    public void Send_routes_a_message_to_the_other_colleague()
    {
        var mediator = new CustomsMediator();
        var declarationDesk = new DeclarationDesk(mediator);
        var dutyDesk = new DutyDesk(mediator);
        mediator.Register(declarationDesk, dutyDesk);

        declarationDesk.Send("Consignment CN-2001 declared.");

        Assert.Contains("Consignment CN-2001 declared.", dutyDesk.Inbox);
    }

    [Fact]
    public void Send_does_not_echo_the_message_back_to_the_sender()
    {
        var mediator = new CustomsMediator();
        var declarationDesk = new DeclarationDesk(mediator);
        var dutyDesk = new DutyDesk(mediator);
        mediator.Register(declarationDesk, dutyDesk);

        declarationDesk.Send("Consignment CN-2002 declared.");

        Assert.Empty(declarationDesk.Inbox);
    }

    [Fact]
    public void Send_routes_a_reply_back_through_the_mediator_without_a_direct_reference()
    {
        var mediator = new CustomsMediator();
        var declarationDesk = new DeclarationDesk(mediator);
        var dutyDesk = new DutyDesk(mediator);
        mediator.Register(declarationDesk, dutyDesk);

        declarationDesk.Send("Consignment CN-2003 declared.");
        dutyDesk.Send("Duty assessed for CN-2003.");

        Assert.Contains("Duty assessed for CN-2003.", declarationDesk.Inbox);
        Assert.DoesNotContain("Duty assessed for CN-2003.", dutyDesk.Inbox);
    }
}
