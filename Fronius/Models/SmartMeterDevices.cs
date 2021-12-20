﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using De.Hochstaetter.Fronius.Contracts;
using De.Hochstaetter.Fronius.Localization;

namespace De.Hochstaetter.Fronius.Models
{
    public class SmartMeterDevices : BaseResponse, IHierarchicalCollection
    {
        IEnumerable IHierarchicalCollection.ItemsEnumerable => SmartMeters;

        IEnumerable IHierarchicalCollection.ChildrenEnumerable => Array.Empty<object>();

        public ICollection<SmartMeter> SmartMeters = new List<SmartMeter>();

        public override string DisplayName => Resources.Meters;
    }
}