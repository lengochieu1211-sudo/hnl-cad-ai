require 'sketchup.rb'
require 'extensions.rb'

module HNL
  module CadAIBridge
    EXTENSION = SketchupExtension.new('HNL CAD AI Bridge', File.join(__dir__, 'hnl_cad_ai_bridge', 'main'))
    EXTENSION.description = 'Bidirectional 2D bridge between SketchUp and HNL CAD AI.'
    EXTENSION.version = '2.7.12'
    EXTENSION.creator = 'HNL'
    Sketchup.register_extension(EXTENSION, true)
  end
end
