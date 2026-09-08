// Retain section links from the classic template without copying its API renderers.
exports.postTransform = function (model) {
  model._sectionAliases = [];
  if (model.syntax) model._sectionAliases.push({ id: model.id + '_syntax', selector: '.codewrapper' });
  if (model.extensionMethods && model.extensionMethods.length)
    model._sectionAliases.push({ id: 'extensionmethods', selector: '.extensionMethods' });
  (model.children || []).forEach(function (group) {
    (group.children || []).forEach(function (member) {
      if (member.seealso && member.seealso.length)
        model._sectionAliases.push({ id: member.id + '_seealso', selector: '#' + member.id + ' ~ .seealso' });
    });
  });
  return model;
};
