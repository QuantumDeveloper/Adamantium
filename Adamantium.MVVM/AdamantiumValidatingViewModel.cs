using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Runtime.CompilerServices;

namespace Adamantium.MVVM;

/// <summary>A view-model base with validation, for <c>[Bindable]</c> fields that carry DataAnnotations attributes; errors
/// surface through <see cref="INotifyDataErrorInfo"/>.</summary>
public abstract class AdamantiumValidatingViewModel : AdamantiumViewModel, INotifyDataErrorInfo
{
    private readonly Dictionary<string, List<string>> _errors = new();

    public bool HasErrors => _errors.Count > 0;

    public event EventHandler<DataErrorsChangedEventArgs> ErrorsChanged;

    public IEnumerable GetErrors(string propertyName)
    {
        if (string.IsNullOrEmpty(propertyName))
            return _errors.SelectMany(static e => e.Value).ToArray();
        return _errors.TryGetValue(propertyName, out var list) ? list.ToArray() : [];
    }

    /// <summary>Validates <paramref name="value"/> against the named property's DataAnnotations attributes and updates
    /// the error set (raising <see cref="ErrorsChanged"/> when it changes). Generated setters call this; it can also
    /// be called manually.</summary>
    protected void ValidateProperty(object value, [CallerMemberName] string propertyName = "")
    {
        List<ValidationResult> results = [];
        Validator.TryValidateProperty(value, new ValidationContext(this) { MemberName = propertyName }, results);

        if (results.Count == 0)
        {
            if (_errors.Remove(propertyName)) OnErrorsChanged(propertyName);
            return;
        }

        _errors[propertyName] = results.Select(static r => r.ErrorMessage).ToList();
        OnErrorsChanged(propertyName);
    }

    protected void OnErrorsChanged(string propertyName)
    {
        ErrorsChanged?.Invoke(this, new DataErrorsChangedEventArgs(propertyName));
        RaisePropertyChanged(nameof(HasErrors));
    }
}
