using Sels.Core.Extensions.Conversion;
using Sels.Core.Extensions.Linq;
using Sels.HiveMind.Colony;
using Sels.HiveMind.Storage.Colony;
using Sels.ObjectValidationFramework.Profile;
using Sels.ObjectValidationFramework.Target;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sels.HiveMind.Validation
{
    /// <summary>
    /// Contains validation rules for objects related to colonies.
    /// </summary>
    public class ColonyValidationProfile : SharedValidationProfile
    {
        /// <inheritdoc cref="ColonyValidationProfile"/>
        public ColonyValidationProfile()
        {
            CreateValidationFor<ColonyStorageData>()
                .ForProperty(x => x.Id, TargetExecutionOptions.ExitOnInvalid)
                    .CannotBeNullOrWhitespace()
                    .MustMatchRegex(HiveMindHelper.Validation.ColonyIdRegex)
                .ForProperty(x => x.Name, TargetExecutionOptions.ExitOnInvalid)
                    .CannotBeNullOrWhitespace()
                    .MustMatchRegex(HiveMindHelper.Validation.DaemonNameRegex)
                    .NextWhen(x => !x.Source.Properties.HasValue() || !x.Source.Properties.TryGetFirst(x => x.Name.HasValue() && x.Name.Equals(HiveMindConstants.Daemon.IsAutoCreatedProperty, StringComparison.OrdinalIgnoreCase), out var property) || !property.StorageValue.CastToOrDefault<bool>()) // Only validate if not auto created
                    .MustNotBeIn(HiveMindHelper.Validation.ReservedDaemonNames, StringComparer.OrdinalIgnoreCase)
                .ForProperty(x => x.Environment, TargetExecutionOptions.ExitOnInvalid)
                    .CannotBeNullOrWhitespace()
                    .MustMatchRegex(HiveMindHelper.Validation.EnvironmentRegex)
                .ForElements(x => x.Daemons)
                    .CannotBeNull()
                .ForElements(x => x.Properties)
                    .CannotBeNull();

            CreateValidationFor<DaemonStorageData>()
                .ForProperty(x => x.Name, TargetExecutionOptions.ExitOnInvalid)
                    .CannotBeNullOrWhitespace()
                    .MustMatchRegex(HiveMindHelper.Validation.DaemonNameRegex)
                .ForProperty(x => x.OriginalInstanceTypeName)
                    .NextWhenNotNull()
                    .CannotBeNullOrWhitespace(x => "Cannot be empty or whitespace when defined")
                .ForProperty(x => x.StateTypeName)
                    .NextWhenNotNull()
                    .CannotBeNullOrWhitespace(x => "Cannot be empty or whitespace when defined")
                .ForProperty(x => x.StateStorageValue)
                    .NextWhenNotNull()
                    .CannotBeNullOrWhitespace(x => "Cannot be empty or whitespace when defined")
                .ForProperty(x => x.StateStorageValue)
                    .NextWhen(x => x.Source.StateTypeName.HasValue())
                    .CannotBeNullOrWhitespace(x => $"Cannot be bull, empty or whitespace when {nameof(x.Source.StateTypeName)} is defined")
                .ForElements(x => x.NewLogEntries)
                    .CannotBeNull()
                .ForElements(x => x.Properties)
                    .CannotBeNull();

            ImportFrom<HiveColonyOptionsValidationProfile>();
        }
    }
}
