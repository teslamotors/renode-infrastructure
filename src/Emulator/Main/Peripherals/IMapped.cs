//
// Copyright (c) 2010-2017 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System.Collections.Generic;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Core;

namespace Antmicro.Renode.Peripherals
{
    public interface IMapped : IBusPeripheral
    {
        IEnumerable<IMappedSegment> MappedSegments { get; }
    }
}

