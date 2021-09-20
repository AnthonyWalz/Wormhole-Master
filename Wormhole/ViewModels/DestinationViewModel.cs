using System;
using System.ComponentModel;
using System.Xml.Serialization;
using Torch;
using Torch.Views;
using VRageMath;

namespace Wormhole.ViewModels
{
    [XmlInclude(typeof(InternalDestinationViewModel))]
    [XmlInclude(typeof(GateDestinationViewModel))]
    public class DestinationViewModel : ViewModel
    {
        [Display(Name = "Display Name")]
        public string DisplayName { get; set; } = "unnamed";

        [Display(Name = "Id", Description = "Must be unique for all destinations in current gate")]
        public string Id { get; set; }
        
        public static DestinationViewModel Create(DestinationType type)
        {
            return type switch
            {
                DestinationType.InternalGps => new InternalDestinationViewModel(),
                DestinationType.Gate => new GateDestinationViewModel(),
                _ => throw new ArgumentOutOfRangeException()
            };
        }
    }

    [Destination(DestinationType.InternalGps)]
    public class InternalDestinationViewModel : DestinationViewModel
    {
        [Display(Name = "Inner Radius", Description = "Inner radius of spawn sphere. Leave 0 to disable.")]
        public int InnerRadius { get; set; }
        [Display(Name = "Outer Radius", Description = "Outer radius of spawn sphere. Leave same as inner to disable.")]
        public int OuterRadius { get; set; }
        [Display(Name = "GPS", Description = "Destination gps for gate. Yes, this will not require exit gate, just gps.")]
        public string Gps { get; set; }

        public Vector3D? TryParsePosition()
        {
            if (Utilities.TryParseGps(Gps, out _, out var pos, out _))
                return pos;
            return null;
        }
    }

    [Destination(DestinationType.Gate)]
    public class GateDestinationViewModel : DestinationViewModel
    {
        [Display(Name = "Destination Gate Name", Description = "Destination gate name. Local or Remote gate works.")]
        public string Name { get; set; }
    }

    public enum DestinationType
    {
        [Description("Local GPS")]
        InternalGps,
        [Description("Wormhole Gate")]
        Gate
    }

    [AttributeUsage(AttributeTargets.Class)]
    public class DestinationAttribute : Attribute
    {
        public DestinationType Type { get; }

        public DestinationAttribute(DestinationType type)
        {
            Type = type;
        }
    }
}