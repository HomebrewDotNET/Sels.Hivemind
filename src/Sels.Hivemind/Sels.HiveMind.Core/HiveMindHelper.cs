using Microsoft.Extensions.Caching.Memory;
using Newtonsoft.Json;
using Sels.Core.Conversion;
using Sels.Core.Conversion.Converters;
using Sels.Core.Conversion.Converters.Simple;
using Sels.Core.Extensions;
using Sels.Core.Extensions.Collections;
using Sels.Core.Extensions.Equality;
using Sels.Core.Extensions.Reflection;
using Sels.HiveMind.Job;
using Sels.HiveMind.Storage;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using Sels.Core.Conversion.Converters.Json;
using Sels.HiveMind.Job.Background;

namespace Sels.HiveMind
{
    /// <summary>
    /// Contains static helper methods.
    /// </summary>
    public static class HiveMindHelper
    {
        /// <summary>
        /// Contains helper methods related to storing state.
        /// </summary>
        public static class Storage
        {
            // Static
            /// <summary>
            /// The type converter used to for converting objects from/to storage formats.
            /// </summary>
            public static GenericConverter StorageConverter { get; } = GenericConverter.DefaultJsonConverter;
            /// <summary>
            /// The custom converters that will be used when converting to/from json.
            /// </summary>
            public static List<Newtonsoft.Json.JsonConverter> JsonConverters { get; } = new List<Newtonsoft.Json.JsonConverter>()
            {
                new JsonTypeConverter()
            };
            /// <summary>
            /// The arguments used for <see cref="StorageConverter"/>
            /// </summary>
            public static IReadOnlyDictionary<string, object> ConverterArguments { get; } = new Dictionary<string, object>()
            {
                { DateTimeConverter.FormatArgument, "o" },
                { Core.Conversion.Converters.Simple.JsonConverter.SettingsArgument, new JsonSerializerSettings() 
                {
                    ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
                    Formatting = Formatting.None,
                    DateTimeZoneHandling = DateTimeZoneHandling.Utc,
                    NullValueHandling = NullValueHandling.Ignore,
                    TypeNameHandling = TypeNameHandling.Auto,
                    TypeNameAssemblyFormatHandling = TypeNameAssemblyFormatHandling.Simple,
                    DefaultValueHandling = DefaultValueHandling.Ignore,
                    Converters = JsonConverters
                }}
            };

            /// <summary>
            /// Converts <paramref name="value"/> into a format for storage.
            /// </summary>
            /// <param name="value">The value to convert</param>
            /// <param name="options">The configured options for the environment</param>
            /// <param name="cache">Optional cache that can be used to speed up conversion</param>
            /// <returns><paramref name="value"/> converted into a format for storage</returns>
            public static string? ConvertToStorageFormat(object value, HiveMindOptions options, IMemoryCache? cache = null)
            {
                options.ValidateArgument(nameof(options));
                if (value == null) return null;

                return StorageConverter.ConvertTo<string>(value, GetConverterArguments(options, cache));
            }

            /// <summary>
            /// Converts <paramref name="value"/> which is in the storage format back to <paramref name="type"/>.
            /// </summary>
            /// <param name="value">The value to convert</param>
            /// <param name="type">The type to convert to</param>
            /// <param name="options">The configured options for the environment</param>
            /// <param name="cache">Optional cache that can be used to speed up conversion</param>
            /// <returns>An instance converted from <paramref name="value"/> to <paramref name="type"/></returns>
            public static object? ConvertFromStorageFormat(string value, Type type, HiveMindOptions options, IMemoryCache? cache = null)
            {
                options.ValidateArgument(nameof(options));
                if (value == null) return type.GetDefaultValue();

                return StorageConverter.ConvertTo(value, type, GetConverterArguments(options, cache));
            }

            /// <summary>
            /// Gets the <see cref="StorageType"/> for <paramref name="value"/>.
            /// </summary>
            /// <param name="value">The instance to get the storage type for</param>
            /// <returns>The storage type for <paramref name="value"/></returns>
            public static StorageType GetStorageType(object value)
            {
                value.ValidateArgument(nameof(value));

                return GetStorageType(value.GetType());
            }

            /// <summary>
            /// Gets the <see cref="StorageType"/> for <paramref name="value"/>.
            /// </summary>
            /// <param name="value">The instance to get the storage type for</param>
            /// <returns>The storage type for <paramref name="value"/></returns>
            public static StorageType GetStorageType(Type valueType)
            {
                valueType.ValidateArgument(nameof(valueType));

                valueType = valueType.GetActualType();

                switch (valueType)
                {
                    case Type boolType when boolType.GetActualType().Is<bool>():
                            return StorageType.Bool;
                    case Type textType when textType.In(typeof(string), typeof(Guid)) || textType.GetActualType().IsEnum:
                        return StorageType.Text;
                    case Type numberType when numberType.GetActualType().In(typeof(short), typeof(int), typeof(long), typeof(byte), typeof(bool)):
                        return StorageType.Number;
                    case Type floatingType when floatingType.GetActualType().In(typeof(decimal), typeof(double), typeof(float), typeof(TimeSpan)):
                        return StorageType.FloatingNumber;
                    case Type dateTime when dateTime.GetActualType().In(typeof(DateTime), typeof(DateTimeOffset)):
                        return StorageType.Date;
                    default:
                        return StorageType.Serialized;
                }
            }

            /// <summary>
            /// Converts <paramref name="value"/> into a format for storage where the conversion is determined by <paramref name="storageType"/>.
            /// </summary>
            /// <param name="value">The value to convert</param>
            /// <returns><paramref name="value"/> converted into a format for storage</returns>
            public static object? ConvertToStorageFormat(StorageType storageType, object value, HiveMindOptions options, IMemoryCache? cache = null)
            {
                options.ValidateArgument(nameof(options));
                if (value == null) return null;

                switch (storageType)
                {
                    case StorageType.Bool:
                        return StorageConverter.ConvertTo<bool>(value, GetConverterArguments(options, cache));
                    case StorageType.Text:
                        return StorageConverter.ConvertTo<string>(value, GetConverterArguments(options, cache));
                    case StorageType.Number:
                        // Convert to long for storage
                        return StorageConverter.ConvertTo<long>(value, GetConverterArguments(options, cache));
                    case StorageType.FloatingNumber:
                        // Convert to double for storage
                        return StorageConverter.ConvertTo<double>(value, GetConverterArguments(options, cache));
                    case StorageType.Date:
                        // Convert to DateTime for storage
                        return StorageConverter.ConvertTo<DateTime>(value, GetConverterArguments(options, cache));
                    default:
                        // In most cases will be converted to json
                        return StorageConverter.ConvertTo<string>(value, GetConverterArguments(options, cache));
                }
            }

            /// <summary>
            /// Converts <paramref name="value"/> which is in the storage format back to <paramref name="type"/> where the conversion is determined by <paramref name="storageType"/>.
            /// </summary>
            /// <param name="value">The value to convert</param>
            /// <param name="type">The type to convert to</param>
            /// <returns>An instance converted from <paramref name="value"/> to <paramref name="type"/></returns>
            public static object ConvertFromStorageFormat(StorageType storageType, object value, Type type, HiveMindOptions options, IMemoryCache? cache = null)
            {
                options.ValidateArgument(nameof(options));
                if (value == null) return type.GetDefaultValue();

                return StorageConverter.ConvertTo(value, type, GetConverterArguments(options, cache));
            }

            /// <summary>
            /// Checks if <paramref name="type"/> is a special type that doesn't have to be stored.
            /// </summary>
            /// <param name="type">The type to check</param>
            /// <returns>True if <paramref name="type"/> is a special type, otherwise false</returns>
            public static bool IsSpecialArgumentType(Type type)
            {
                type.ValidateArgument(nameof(type));

                switch (type)
                {
                    case Type backgroundJobContext when backgroundJobContext.IsAssignableTo<IBackgroundJobExecutionContext>(): return true;
                    case Type recurringJobContext when recurringJobContext.IsAssignableTo<IBackgroundJobExecutionContext>(): return true;
                    case Type cancellationTokenType when cancellationTokenType.IsAssignableTo<CancellationToken>(): return true;
                    default: return false;
                }
            }

            private static IReadOnlyDictionary<string, object> GetConverterArguments(HiveMindOptions options, IMemoryCache? cache = null)
            {
                options.ValidateArgument(nameof(options));

                if(cache != null)
                {
                    var arguments = new Dictionary<string, object>(ConverterArguments);
                    arguments.AddOrUpdate(ConversionConstants.Converters.CacheArgument, cache);
                    arguments.AddOrUpdate(ConversionConstants.Converters.CacheKeyPrefixArgument, options.CachePrefix);
                    arguments.AddOrUpdate(ConversionConstants.Converters.CacheRetentionArgument, options.TypeConversionCacheRetention);
                    return arguments;
                }

                return ConverterArguments;
            }
        }

        /// <summary>
        /// Contains helpers methods for validating various HiveMind related things.
        /// </summary>
        public static class Validation
        {
            // Constants
            /// <summary>
            /// The regex that a queue name must match.
            /// </summary>
            public const string QueueNameRegex = @"^([A-Za-z0-9.\-_]){1,255}";
            /// <summary>
            /// The regex that an environment must match.
            /// </summary>
            public const string EnvironmentRegex = "^([A-Za-z0-9]){1,64}";
            /// <summary>
            /// The regex that a colony id must match.
            /// </summary>
            public const string ColonyIdRegex = @"^([A-Za-z0-9.\-_]){1,255}";
            /// <summary>
            /// The regex that a colony daemon name must match.
            /// </summary>
            public const string DaemonNameRegex = @"^\$?([A-Za-z0-9.\-_]){1,254}";
            /// <summary>
            /// The regex that a recurring job id must match.
            /// </summary>
            public const string RecurringJobIdentifierRegex = @"^([A-Za-z0-9.\-_]){1,255}";
            /// <summary>
            /// The list of reserved daemon names.
            /// </summary>
            public static IReadOnlyList<string> ReservedDaemonNames { get; } = new List<string>()
            {
                "$ColonyStateSyncService",
                "$DeletionDaemon",
                "$LockMonitorDaemon",
                "$System.RecurringJobSwarmHost"
            };

            /// <summary>
            /// Checks that <paramref name="queue"/> is a valid queue name.
            /// Will throw a <see cref="ArgumentException"/> if the queue is not valid.
            /// </summary>
            /// <param name="queue">The queue to validate</param>
            public static void ValidateQueueName(string queue)
            {
                queue.ValidateArgumentNotNullOrWhitespace(nameof(queue));
                if (!Regex.IsMatch(queue, QueueNameRegex)) throw new ArgumentException($"{nameof(queue)} must match regex {QueueNameRegex}");
            }

            /// <summary>
            /// Checks that <paramref name="environment"/> is a valid environment name.
            /// Will throw a <see cref="ArgumentException"/> if the queue is not valid.
            /// </summary>
            /// <param name="environment">The environment to validate</param>
            public static void ValidateEnvironment(string environment)
            {
                environment.ValidateArgumentNotNullOrWhitespace(nameof(environment));
                if (!Regex.IsMatch(environment, EnvironmentRegex)) throw new ArgumentException($"{nameof(environment)} must match regex {EnvironmentRegex}");
            }

            /// <summary>
            /// Checks that <paramref name="id"/> is a valid environment name.
            /// Will throw a <see cref="ArgumentException"/> if the id is not valid.
            /// </summary>
            /// <param name="id">The environment to validate</param>
            public static void ValidateRecurringJobId(string id)
            {
                id.ValidateArgumentNotNullOrWhitespace(nameof(id));
                if (!Regex.IsMatch(id, RecurringJobIdentifierRegex)) throw new ArgumentException($"{nameof(id)} must match regex {RecurringJobIdentifierRegex}");
            }

            /// <summary>
            /// Checks that <paramref name="id"/> is a valid colony id.
            /// Will throw a <see cref="ArgumentException"/> if the name is not valid.
            /// </summary>
            /// <param name="name">The queue to validate</param>
            public static void ValidateColonyId(string id)
            {
                id.ValidateArgumentNotNullOrWhitespace(nameof(id));
                if (!Regex.IsMatch(id, ColonyIdRegex)) throw new ArgumentException($"{nameof(id)} must match regex {ColonyIdRegex}");
            }
            /// <summary>
            /// Checks that <paramref name="name"/> is a valid daemon name.
            /// Will throw a <see cref="ArgumentException"/> if the name is not valid.
            /// </summary>
            /// <param name="id">The name to validate</param>
            public static void ValidateDaemonName(string name)
            {
                name.ValidateArgumentNotNullOrWhitespace(nameof(name));
                if (!Regex.IsMatch(name, DaemonNameRegex)) throw new ArgumentException($"{nameof(name)} must match regex {DaemonNameRegex}");
                if (ReservedDaemonNames.Contains(name, StringComparer.OrdinalIgnoreCase)) throw new ArgumentException($"{nameof(name)} is a reserved daemon name");
            }
        }
    }
}
