using System;
using System.Collections.Generic;
using System.Text;

using ChannelMediator;

using ChannelMediatorSampleShared;

namespace AzureBusReaderSampleReaderConsole;

internal class MyRequestHandler
	: IRequestHandler<MyRequest>
{
	public Task Handle(MyRequest request, CancellationToken cancellationToken)
	{
		Console.WriteLine(request.Message);
		return Task.CompletedTask;
	}
}
