require 'sketchup.rb'
require 'json'
require 'time'

module HNL
  module CadAIBridge
    DICT = 'HNL_CAD_AI'
    VERSION = '2.7.3'
    MM_PER_INCH = 25.4

    def self.mm_point(pt)
      { 'x' => pt.x.to_f * MM_PER_INCH, 'y' => pt.y.to_f * MM_PER_INCH, 'z' => pt.z.to_f * MM_PER_INCH }
    end

    def self.link_id(entity)
      id = entity.get_attribute(DICT, 'link_id')
      unless id
        id = "su_#{entity.persistent_id}"
        entity.set_attribute(DICT, 'link_id', id)
      end
      id
    end

    def self.tag_name(entity)
      lyr = entity.respond_to?(:layer) ? entity.layer : nil
      lyr ? lyr.name.to_s : 'Untagged'
    end

    def self.collect_edges(entities, tr, path, out, model, section_plane = nil)
      entities.each do |ent|
        next unless ent.valid?
        if ent.is_a?(Sketchup::Edge)
          p1 = ent.start.position.transform(tr)
          p2 = ent.end.position.transform(tr)
          cross_section = false
          if section_plane
            pl = section_plane.get_plane
            d1 = p1.distance_to_plane(pl)
            d2 = p2.distance_to_plane(pl)
            cross_section = (d1 == 0.0 || d2 == 0.0 || d1 * d2 < 0.0)
          end
          out << {
            'id' => ent.persistent_id.to_s,
            'linkId' => link_id(ent),
            'p1' => mm_point(p1),
            'p2' => mm_point(p2),
            'tag' => tag_name(ent),
            'hidden' => ent.hidden?,
            'soft' => ent.soft?,
            'smooth' => ent.smooth?,
            'sourceClass' => ent.hidden? ? 'HIDDEN' : 'VISIBLE',
            'sectionCrossing' => cross_section,
            'path' => path
          }
        elsif ent.is_a?(Sketchup::Group)
          collect_edges(ent.entities, tr * ent.transformation, path + [ent.name.to_s], out, model, section_plane)
        elsif ent.is_a?(Sketchup::ComponentInstance)
          collect_edges(ent.definition.entities, tr * ent.transformation, path + [ent.definition.name.to_s], out, model, section_plane)
        end
      end
    end

    def self.active_scene_info(model)
      page = model.pages.selected_page
      camera = page ? page.camera : model.active_view.camera
      section = nil
      if page && page.respond_to?(:active_section_planes)
        arr = page.active_section_planes
        section = arr && arr.first
      end
      section ||= model.active_entities.active_section_plane if model.active_entities.respond_to?(:active_section_plane)
      [page, camera, section]
    end

    def self.export_scene_package
      model = Sketchup.active_model
      page, camera, section = active_scene_info(model)
      edges = []
      collect_edges(model.entities, Geom::Transformation.new, [], edges, model, section)
      tags = model.layers.map { |l| { 'name' => l.name.to_s, 'visible' => l.visible? } }
      sec = nil
      if section
        pl = section.get_plane
        sec = { 'name' => section.name.to_s, 'plane' => pl.map(&:to_f) }
      end
      data = {
        'schema' => 'hnl-sketchup-scene', 'version' => 1, 'bridgeVersion' => VERSION,
        'generatedAt' => Time.now.utc.iso8601, 'modelName' => File.basename(model.path.to_s),
        'sceneName' => page ? page.name.to_s : 'Current View', 'units' => 'mm',
        'camera' => {
          'eye' => mm_point(camera.eye), 'target' => mm_point(camera.target),
          'up' => { 'x' => camera.up.x.to_f, 'y' => camera.up.y.to_f, 'z' => camera.up.z.to_f },
          'perspective' => camera.perspective?, 'fov' => camera.respond_to?(:fov) ? camera.fov.to_f : nil
        },
        'sectionPlane' => sec, 'edges' => edges, 'tags' => tags
      }
      path = UI.savepanel('Export HNL SketchUp Scene', nil, 'HNL_SketchUp_Scene.json')
      return unless path
      File.write(path, JSON.pretty_generate(data))
      UI.messagebox("HNL CAD AI: Exported #{edges.length} edges.\n#{path}")
    rescue => e
      UI.messagebox("HNL Export error: #{e.message}\n#{e.backtrace&.first(5)&.join("\n")}")
    end

    def self.import_hnl_cad
      path = UI.openpanel('Open HNL CAD → SketchUp package', nil, 'JSON|*.json||')
      return unless path
      data = JSON.parse(File.read(path))
      raise 'Unsupported bridge schema' unless data['schema'] == 'hnl-sketchup-2d-bridge'
      model = Sketchup.active_model
      model.start_operation('HNL CAD Import/Update', true)
      root = model.entities.find { |e| e.is_a?(Sketchup::Group) && e.get_attribute(DICT, 'root') == true }
      root ||= model.entities.add_group
      root.name = 'HNL_CAD_IMPORT'
      root.set_attribute(DICT, 'root', true)
      index = {}
      root.entities.each { |e| index[e.get_attribute(DICT, 'link_id')] = e if e.respond_to?(:get_attribute) }
      added = updated = 0
      data['geometry'].each do |g|
        lid = g['id'].to_s
        old = index[lid]
        old.erase! if old && old.valid?
        d = g['data'] || {}
        created = []
        case g['type']
        when 'LINE'
          created << root.entities.add_line([d['start']['x'].mm,d['start']['y'].mm,0], [d['end']['x'].mm,d['end']['y'].mm,0])
        when 'POLYLINE'
          pts=(d['points']||[]).map{|p| [p['x'].mm,p['y'].mm,0]}; created += Array(root.entities.add_curve(pts)) if pts.length>1
          created << root.entities.add_line(pts[-1],pts[0]) if d['closed'] && pts.length>2
        when 'RECTANGLE'
          x=d['x'].mm;y=d['y'].mm;w=d['width'].mm;h=d['height'].mm
          created += Array(root.entities.add_curve([[x,y,0],[x+w,y,0],[x+w,y+h,0],[x,y+h,0],[x,y,0]]))
        when 'CIRCLE'
          created += Array(root.entities.add_circle([d['center']['x'].mm,d['center']['y'].mm,0],[0,0,1],d['radius'].mm))
        end
        tag_name = g['tag'].to_s.empty? ? 'CAD_IMPORT' : g['tag'].to_s
        tag = model.layers[tag_name] || model.layers.add(tag_name)
        created.compact.each { |e| e.layer = tag; e.set_attribute(DICT, 'link_id', lid) }
        old ? updated += 1 : added += 1
      end
      model.commit_operation
      model.active_view.zoom_extents
      UI.messagebox("HNL CAD Import completed.\nAdded: #{added}\nUpdated: #{updated}")
    rescue => e
      model.abort_operation if model
      UI.messagebox("HNL Import error: #{e.message}\n#{e.backtrace&.first(5)&.join("\n")}")
    end

    def self.export_native_dxf
      model = Sketchup.active_model
      path = UI.savepanel('Export Native DXF (SketchUp Pro)', nil, 'HNL_SketchUp_Native.dxf')
      return unless path
      options = {
        :acad_version => 'acad_2013', :faces_flag => false, :construction_geometry => false,
        :dimensions => false, :text => false, :edges => true, :materials => false, :show_summary => true
      }
      ok = model.export(path, options)
      UI.messagebox(ok ? "Native DXF exported:\n#{path}" : 'SketchUp DXF exporter returned false.')
    rescue => e
      UI.messagebox("Native DXF export error: #{e.message}")
    end

    unless file_loaded?(__FILE__)
      menu = UI.menu('Extensions').add_submenu('HNL CAD AI Bridge')
      menu.add_item('Export Scene/Section → HNL CAD') { export_scene_package }
      menu.add_item('Import/Update HNL CAD → SketchUp') { import_hnl_cad }
      menu.add_separator
      menu.add_item('Export Native DXF (SketchUp Pro)') { export_native_dxf }
      file_loaded(__FILE__)
    end
  end
end
